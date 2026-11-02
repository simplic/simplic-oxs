using MongoDB.Driver;

namespace Simplic.OxS.Data.MongoDB.Abac;

/// <summary>
/// MongoDB implementation of <see cref="IAbacRuleStore"/> on the collection <c>abac.compiled-rules</c>.
/// <para>
/// Activation: the new set is upserted with <c>IsActive = true</c> first, then all other active
/// sets of the organization are deactivated. During the short overlap readers pick the most
/// recently fetched active set, so there is never a window without an active set.
/// </para>
/// </summary>
public class MongoAbacRuleStore : IAbacRuleStore
{
    /// <summary>Collection name of the service-local rule store.</summary>
    public const string CollectionName = "abac.compiled-rules";

    private static readonly object IndexLock = new();
    private static readonly HashSet<string> IndexedDatabases = [];

    private readonly IMongoContext _context;
    private IMongoCollection<AbacStoredRuleSet>? _collection;

    /// <summary>Initializes a new instance of <see cref="MongoAbacRuleStore"/>.</summary>
    public MongoAbacRuleStore(IMongoContext context)
    {
        _context = context;
    }

    private IMongoCollection<AbacStoredRuleSet> Collection
        => _collection ??= _context.GetCollection<AbacStoredRuleSet>(CollectionName);

    /// <inheritdoc/>
    public async Task<AbacStoredRuleSet?> GetActiveAsync(Guid organizationId, CancellationToken ct = default)
    {
        var filter = Builders<AbacStoredRuleSet>.Filter.Eq(x => x.OrganizationId, organizationId)
                   & Builders<AbacStoredRuleSet>.Filter.Eq(x => x.IsActive, true);

        return await Collection.Find(filter)
            .SortByDescending(x => x.FetchedAt)
            .Limit(1)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task SaveActiveAsync(AbacStoredRuleSet ruleSet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        if (ruleSet.OrganizationId == Guid.Empty)
            throw new ArgumentException("Rule set must be organization-scoped.", nameof(ruleSet));

        await EnsureIndexesAsync(ct);

        var f = Builders<AbacStoredRuleSet>.Filter;
        ruleSet.Id = AbacStoredRuleSet.BuildId(ruleSet.OrganizationId, ruleSet.SetId);
        ruleSet.IsActive = true;

        // 1) Write the new set completely (active).
        await Collection.ReplaceOneAsync(f.Eq(x => x.Id, ruleSet.Id), ruleSet, new ReplaceOptions { IsUpsert = true }, ct);

        // 2) Deactivate all other sets of the organization.
        await Collection.UpdateManyAsync(
            f.Eq(x => x.OrganizationId, ruleSet.OrganizationId) & f.Ne(x => x.Id, ruleSet.Id) & f.Eq(x => x.IsActive, true),
            Builders<AbacStoredRuleSet>.Update.Set(x => x.IsActive, false),
            cancellationToken: ct);

        // 3) Retention: keep active + the most recent previous set only.
        var inactiveIds = await Collection
            .Find(f.Eq(x => x.OrganizationId, ruleSet.OrganizationId) & f.Eq(x => x.IsActive, false))
            .SortByDescending(x => x.FetchedAt)
            .Project(x => x.Id)
            .ToListAsync(ct);

        if (inactiveIds.Count > 1)
            await Collection.DeleteManyAsync(f.In(x => x.Id, inactiveIds.Skip(1)), ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AbacStoredRuleSet>> GetActiveSetsFetchedBeforeAsync(DateTime fetchedBeforeUtc, CancellationToken ct = default)
    {
        var f = Builders<AbacStoredRuleSet>.Filter;
        var result = await Collection
            .Find(f.Eq(x => x.IsActive, true) & f.Lt(x => x.FetchedAt, fetchedBeforeUtc))
            .Project<AbacStoredRuleSet>(Builders<AbacStoredRuleSet>.Projection.Exclude(x => x.PayloadJson))
            .ToListAsync(ct);

        return result;
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        var key = Collection.Database.DatabaseNamespace.DatabaseName;
        lock (IndexLock)
        {
            if (IndexedDatabases.Contains(key))
                return;
        }

        var keys = Builders<AbacStoredRuleSet>.IndexKeys;
        await Collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AbacStoredRuleSet>(keys.Ascending(x => x.OrganizationId).Ascending(x => x.IsActive)),
            new CreateIndexModel<AbacStoredRuleSet>(keys.Ascending(x => x.OrganizationId).Ascending(x => x.SetId)),
            new CreateIndexModel<AbacStoredRuleSet>(keys.Ascending(x => x.IsActive).Ascending(x => x.FetchedAt))
        ], ct);

        lock (IndexLock)
            IndexedDatabases.Add(key);
    }
}

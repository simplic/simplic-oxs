using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Simplic.OxS.Data.MongoDB;

namespace Simplic.OxS.ServiceDefinition.Repository;

/// <summary>
/// MongoDB implementation of the <see cref="IAddonDefinitionRepository"/>.
/// <para>
/// The collection carries the service's name (<see cref="CollectionNameOf"/>): several services
/// are deployed onto one database, and a definition belongs to the service that stores the
/// values. No read and no write of one service ever sees a row of another, with no filter that
/// could be forgotten.
/// </para>
/// <para>
/// One organisation holds at most one row per entity id and path. A unique index says so
/// (<see cref="UniqueIndex"/>); it is created when this process first writes to the collection,
/// and a write that would store a second row is a <see cref="AddonDefinitionConflictException"/>.
/// </para>
/// </summary>
public class AddonDefinitionRepository(IMongoContext context, IRequestContext requestContext, ICurrentService currentService, ILogger<AddonDefinitionRepository>? logger = null) :
    MongoOrganizationRepositoryBase<AddonDefinitionDocument, AddonDefinitionFilter>(context, requestContext),
    IAddonDefinitionRepository
{
    /// <summary>The collection name every service's collection starts with.</summary>
    public const string CollectionNamePrefix = "model_definition.addon_definition";

    /// <summary>The name of the unique index over organisation, entity id and path.</summary>
    public const string UniqueIndexName = "organization_entity_path_unique";

    /// <summary>The collections this process has created the index on, by <c>database.collection</c>.</summary>
    private static readonly ConcurrentDictionary<string, bool> Indexed = new(StringComparer.Ordinal);

    private readonly string collectionName = CollectionNameOf(currentService.ServiceName);

    /// <summary>
    /// The collection of one service: <c>model_definition.addon_definition.{service}</c>, the
    /// service name trimmed and lower-cased, as the scheduler names its collections
    /// (<c>hangfire.{service}</c>) and the host its api (<c>{service}-api</c>).
    /// </summary>
    /// <param name="serviceName">The host's service name.</param>
    /// <exception cref="InvalidOperationException">The host names no service.</exception>
    public static string CollectionNameOf(string? serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new InvalidOperationException("Addon definitions are stored per service, and the host names no service (ICurrentService.ServiceName).");

        return $"{CollectionNamePrefix}.{serviceName.Trim().ToLowerInvariant()}";
    }

    /// <summary>
    /// The unique index: one row per organisation, entity id and path, among the rows that are
    /// not deleted. A retired definition is a row like any other and keeps its place, so its
    /// path cannot be stored a second time; it is revived. A deleted row is outside the index and
    /// outside every read, so it never blocks its path.
    /// </summary>
    public static CreateIndexModel<AddonDefinitionDocument> UniqueIndex() => new(
        Builders<AddonDefinitionDocument>.IndexKeys
            .Ascending(definition => definition.OrganizationId)
            .Ascending(definition => definition.Entity)
            .Ascending(definition => definition.Path),
        new CreateIndexOptions<AddonDefinitionDocument>
        {
            Name = UniqueIndexName,
            Unique = true,
            PartialFilterExpression = Builders<AddonDefinitionDocument>.Filter.Eq(definition => definition.IsDeleted, false),
        });

    protected override IEnumerable<FilterDefinition<AddonDefinitionDocument>> GetFilterQueries(AddonDefinitionFilter filter)
    {
        if (filter.Entity is not null)
            yield return Builders<AddonDefinitionDocument>.Filter.Eq(s => s.Entity, filter.Entity);

        if (filter.Entities is not null)
            yield return Builders<AddonDefinitionDocument>.Filter.In(s => s.Entity, filter.Entities);

        if (filter.Path is not null)
            yield return Builders<AddonDefinitionDocument>.Filter.Eq(s => s.Path, filter.Path);

        if (filter.Retired is { } retired)
            yield return Builders<AddonDefinitionDocument>.Filter.Eq(s => s.Retired, retired);

        foreach (var definition in base.GetFilterQueries(filter))
            yield return definition;
    }

    protected override string GetCollectionName() => collectionName;

    /// <inheritdoc/>
    public async Task<IEnumerable<AddonDefinitionDocument>> GetByEntitiesAsync(IReadOnlyCollection<string> entities, Guid? organizationId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        // The base read takes no token, so a request cancelled before the read never starts it.
        cancellationToken.ThrowIfCancellationRequested();

        if (entities.Count == 0)
            return [];

        return await GetByFilterAsync(new AddonDefinitionFilter
        {
            Entities = [.. entities],
            OrganizationId = organizationId,
            IsDeleted = false,
        });
    }

    /// <summary>
    /// Creates the unique index unless this process already has. Creating an index that exists
    /// as specified is a no-op at the server, so every replica and every restart may ask. An
    /// index that cannot be created (rows that already break it, an index of that name with
    /// other options, no right to create one) is logged and asked for again at the next write;
    /// the write goes on, guarded by the caller's own read as before.
    /// </summary>
    /// <param name="cancellationToken">A token to stop waiting with.</param>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Initialize();

        var key = Collection.CollectionNamespace.FullName;

        if (Indexed.ContainsKey(key))
            return;

        try
        {
            await Collection.Indexes.CreateOneAsync(UniqueIndex(), cancellationToken: cancellationToken);
            Indexed[key] = true;
        }
        catch (MongoException exception)
        {
            logger?.LogWarning(exception, "The unique index {Index} on {Collection} could not be created; one path may be stored twice until it is.", UniqueIndexName, key);
        }
    }

    /// <summary>
    /// Writes what was added, the unique index in place first. A write the index refuses is a
    /// <see cref="AddonDefinitionConflictException"/>.
    /// </summary>
    /// <exception cref="AddonDefinitionConflictException">The organisation already holds a row of that entity id and path.</exception>
    public override async Task<int> CommitAsync()
    {
        await EnsureIndexesAsync();

        try
        {
            return await base.CommitAsync();
        }
        catch (MongoException exception) when (IsDuplicateKey(exception))
        {
            throw new AddonDefinitionConflictException("The organisation already holds a definition of that entity and path.", exception);
        }
    }

    /// <summary>Whether the server refused a write because of a unique index.</summary>
    public static bool IsDuplicateKey(MongoException exception) => exception switch
    {
        MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoBulkWriteException bulk => bulk.WriteErrors.Any(error => error.Category == ServerErrorCategory.DuplicateKey),
        MongoCommandException command => command.Code is 11000 or 11001,
        _ => false,
    };
}

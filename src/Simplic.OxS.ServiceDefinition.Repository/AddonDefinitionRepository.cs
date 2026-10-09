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
/// Where the server refuses to create the index (the service's account may not create one, rows
/// already break it) the writes go on without it, the failure is logged as a warning by the write
/// that met it first (by each of them where several wrote at the same moment), and the creation is
/// asked for again only after <see cref="IndexRetryAfter"/>, not at every write. Where the command
/// did not reach a server that could answer it (a connection lost, an election under way) nothing
/// is concluded and the next write asks again. <see cref="IndexStateOf"/> says which state a
/// collection is in.
/// </para>
/// </summary>
public class AddonDefinitionRepository(IMongoContext context, IRequestContext requestContext, ICurrentService currentService, ILogger<AddonDefinitionRepository>? logger = null, TimeProvider? time = null) :
    MongoOrganizationRepositoryBase<AddonDefinitionDocument, AddonDefinitionFilter>(context, requestContext),
    IAddonDefinitionRepository
{
    /// <summary>The collection name every service's collection starts with.</summary>
    public const string CollectionNamePrefix = "model_definition.addon_definition";

    /// <summary>The name of the unique index over organisation, entity id and path.</summary>
    public const string UniqueIndexName = "organization_entity_path_unique";

    /// <summary>
    /// How long a failed creation of the unique index keeps the next one from being asked for. The
    /// causes do not pass by themselves (a missing right, rows that break the index, an index of the
    /// name with other options), so a write does not pay a refused command each time; an hour later
    /// one write asks again, which is how the index appears without a restart once the cause is gone.
    /// </summary>
    public static readonly TimeSpan IndexRetryAfter = TimeSpan.FromHours(1);

    /// <summary>What this process knows of the unique index of each collection it wrote to, by <c>database.collection</c>.</summary>
    private static readonly ConcurrentDictionary<string, AddonDefinitionIndexState> Indexes = new(StringComparer.Ordinal);

    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private string? collectionName;

    /// <summary>
    /// What this process knows of the unique index of a collection (<c>database.collection</c>): null
    /// before its first write, else whether the index is in place or its creation failed, when, and
    /// why. While it failed, uniqueness rests on the caller's own read before its write.
    /// </summary>
    public static AddonDefinitionIndexState? IndexStateOf(string collectionNamespace) =>
        Indexes.TryGetValue(collectionNamespace, out var state) ? state : null;

    /// <summary>Every collection this process wrote to, with the state of its unique index: what a host reports of them.</summary>
    public static IReadOnlyDictionary<string, AddonDefinitionIndexState> IndexStates => Indexes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

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

    /// <summary>
    /// The service's collection. Named when it is first needed, not when the repository is built: a
    /// host that names no service fails the reads and writes of addon definitions, not every request
    /// whose services happen to hold a repository.
    /// </summary>
    protected override string GetCollectionName() => collectionName ??= CollectionNameOf(currentService.ServiceName);

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
    /// index the server refuses to create (rows that already break it, an index of that name with
    /// other options, no right to create one) is logged as a warning by the first write that met
    /// the refusal and asked for again only after <see cref="IndexRetryAfter"/>: the writes in
    /// between send no command that is refused. A command that failed for another reason than a
    /// refusal (the connection, a primary stepping down, a write concern not met in time) says
    /// nothing about the index: the next write asks again. The write goes on either way, guarded
    /// by the caller's own read as before.
    /// </summary>
    /// <param name="cancellationToken">A token to stop waiting with.</param>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Initialize();

        var key = Collection.CollectionNamespace.FullName;
        var known = IndexStateOf(key);

        if (known is { Created: true } || (known is { FailedAt: { } failed } && clock.GetUtcNow() - failed < IndexRetryAfter))
            return;

        try
        {
            await Collection.Indexes.CreateOneAsync(UniqueIndex(), cancellationToken: cancellationToken);
            Indexes[key] = new AddonDefinitionIndexState(Created: true, FailedAt: null, Reason: null);

            if (known is not null)
                logger?.LogInformation("The unique index {Index} on {Collection} is in place now.", UniqueIndexName, key);
        }
        catch (MongoException exception) when (!IsRefusal(exception))
        {
            // Not an answer about the index: nothing is recorded, so the next write asks again.
            logger?.LogDebug(exception, "The unique index {Index} on {Collection} could not be asked for; the next write asks again.", UniqueIndexName, key);
        }
        catch (MongoException exception)
        {
            Indexes[key] = new AddonDefinitionIndexState(Created: false, FailedAt: clock.GetUtcNow(), Reason: exception.GetType().Name + ": " + exception.Message);

            // Said once per process and collection: a later attempt that fails the same way says it at debug level.
            if (known is null)
                logger?.LogWarning(exception, "The unique index {Index} on {Collection} could not be created; one path may be stored twice until it is. The creation is asked for again in {Retry}, not at every write.", UniqueIndexName, key, IndexRetryAfter);
            else
                logger?.LogDebug(exception, "The unique index {Index} on {Collection} still cannot be created.", UniqueIndexName, key);
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

    /// <summary>
    /// Whether a failed index creation is the server's refusal of the command, which waiting an hour
    /// answers, and not a failure to reach a server that could answer it, which the next write may
    /// not meet: a connection or its timeout, a node that is no primary or is recovering, a write
    /// concern not met in time, a command the server ran out of time for.
    /// </summary>
    public static bool IsRefusal(MongoException exception) => exception is not
        (MongoConnectionException or MongoNotPrimaryException or MongoNodeIsRecoveringException or MongoWriteConcernException or MongoExecutionTimeoutException or MongoInternalException);

    /// <summary>Whether the server refused a write because of a unique index.</summary>
    public static bool IsDuplicateKey(MongoException exception) => exception switch
    {
        MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoBulkWriteException bulk => bulk.WriteErrors.Any(error => error.Category == ServerErrorCategory.DuplicateKey),
        MongoCommandException command => command.Code is 11000 or 11001,
        _ => false,
    };
}

/// <summary>What a process knows of the unique index of one addon definition collection.</summary>
/// <param name="Created">Whether the index is in place: this process created it, or found it as specified.</param>
/// <param name="FailedAt">When its creation last failed; null while <paramref name="Created"/>.</param>
/// <param name="Reason">The server's or the driver's reason for the failure; null while <paramref name="Created"/>.</param>
public sealed record AddonDefinitionIndexState(bool Created, DateTimeOffset? FailedAt, string? Reason);

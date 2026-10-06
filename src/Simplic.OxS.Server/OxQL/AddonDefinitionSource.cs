using System.Diagnostics;
using OxQL.Model;
using OxQL.Model.Addon;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.ServiceDefinition;
using Simplic.OxS.ServiceDefinition.Repository;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// The query engine's per-request view of an organisation's addon definitions, read from the
/// repository through <see cref="AddonDefinitionCache"/>. An entity's definitions are the
/// union of the rows stored under its current id and under every id it retired, so a service
/// that renames an entity keeps its definitions. Retired definitions are included, as the
/// engine's contract asks; a stored kind the engine does not know is read as
/// <see cref="AddonKind.Object"/>, an untyped container.
/// <para>
/// A call that had to wait for the repository says so to the engine
/// (<see cref="IReportingAddonDefinitionSource"/>): the wait is a command at the database in the
/// request's timing, of kind <c>addon</c>, and not the service's own time. A call the cache
/// answered reports nothing.
/// </para>
/// </summary>
public sealed class AddonDefinitionSource(IAddonDefinitionRepository repository, AddonDefinitionCache cache, OxSchemaRegistry schema, ICurrentService? service = null) : IReportingAddonDefinitionSource
{
    /// <summary>The collection the definitions are read from, as a request's timing names it.</summary>
    private readonly string collection = string.IsNullOrWhiteSpace(service?.ServiceName)
        ? AddonDefinitionRepository.CollectionNamePrefix
        : AddonDefinitionRepository.CollectionNameOf(service.ServiceName);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<AddonDefinition>> ForEntityAsync(string entity, Guid organisation, CancellationToken cancellationToken) =>
        ForEntityAsync(entity, organisation, null, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<AddonDefinition>> ForEntityAsync(string entity, Guid organisation, AddonReadReport? report, CancellationToken cancellationToken)
    {
        var current = CurrentId(schema.Model, entity);
        var started = Stopwatch.GetTimestamp();
        var lookup = cache.GetAsync(organisation, current, token => LoadAsync(repository, schema.Model, current, organisation, token), cancellationToken);

        // The cache answered: nothing was waited for, nothing is reported.
        return lookup.IsCompletedSuccessfully && lookup.Result.Waited == AddonDefinitionWait.None
            ? ValueTask.FromResult(lookup.Result.Definitions)
            : WaitedAsync(lookup, started, report);
    }

    private async ValueTask<IReadOnlyList<AddonDefinition>> WaitedAsync(ValueTask<AddonDefinitionLookup> lookup, long started, AddonReadReport? report)
    {
        var (definitions, waited) = await lookup;

        // A read another request had sent cost this one the wait and no round trip.
        if (waited != AddonDefinitionWait.None)
            report?.Database(collection, started, definitions.Count, waited == AddonDefinitionWait.Read ? 1 : 0);

        return definitions;
    }

    /// <summary>
    /// The engine's records of every stored definition of an entity, under its current id
    /// (<see cref="ReadAsync"/>): what the cache holds.
    /// </summary>
    /// <param name="repository">The definition repository.</param>
    /// <param name="model">The entity model that knows the retired ids.</param>
    /// <param name="current">The entity's current id.</param>
    /// <param name="organisation">The organisation.</param>
    /// <param name="cancellationToken">A token to stop the read with.</param>
    public static async Task<IReadOnlyList<AddonDefinition>> LoadAsync(IAddonDefinitionRepository repository, EntityModel model, string current, Guid organisation, CancellationToken cancellationToken)
    {
        var documents = await ReadAsync(repository, model, current, organisation, cancellationToken);

        return documents.Select(document => ToDefinition(document) with { Entity = current }).ToList();
    }

    /// <summary>
    /// The current id of <paramref name="entity"/>: itself when it is a live entity or unknown,
    /// the entity that replaced it when it is a retired id.
    /// </summary>
    public static string CurrentId(EntityModel model, string entity) =>
        !string.IsNullOrWhiteSpace(entity) && model.TryResolve(entity, out var definition, out _) ? definition.Id : entity;

    /// <summary>
    /// Every stored definition of an entity, read in one query over its current id and every id
    /// it retired: the rows under its current id first, then the rows under each retired id in
    /// ordinal order (<see cref="Union"/>).
    /// </summary>
    /// <param name="repository">The definition repository.</param>
    /// <param name="model">The entity model that knows the retired ids.</param>
    /// <param name="entity">The entity's current or retired id.</param>
    /// <param name="organisation">The organisation, or null for the current request's.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    public static async Task<IReadOnlyList<AddonDefinitionDocument>> ReadAsync(IAddonDefinitionRepository repository, EntityModel model, string entity, Guid? organisation, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> ids = model.TryResolve(entity, out var definition, out _)
            ? [definition.Id, .. definition.RetiredIds]
            : [entity];

        return Union(ids, await repository.GetByEntitiesAsync(ids, organisation, cancellationToken));
    }

    /// <summary>
    /// One row per path out of the rows stored under <paramref name="ids"/>, in the order of the
    /// ids and then of the rows. A path stored under more than one id is read from a live row
    /// before a retired one, and between rows alike from the id listed first, so the current id
    /// wins over a retired one and a retired row never hides a live one.
    /// </summary>
    /// <param name="ids">The entity's current id, then its retired ids.</param>
    /// <param name="rows">The rows stored under any of them.</param>
    public static IReadOnlyList<AddonDefinitionDocument> Union(IReadOnlyList<string> ids, IEnumerable<AddonDefinitionDocument> rows)
    {
        var rank = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < ids.Count; index++)
            rank.TryAdd(ids[index], index);

        var chosen = new Dictionary<string, AddonDefinitionDocument>(StringComparer.Ordinal);
        var paths = new List<string>();

        foreach (var row in rows.Where(row => row.Entity is not null && rank.ContainsKey(row.Entity)).OrderBy(row => rank[row.Entity]))
        {
            if (!chosen.TryGetValue(row.Path, out var held))
            {
                chosen[row.Path] = row;
                paths.Add(row.Path);
            }
            else if (held.Retired && !row.Retired)
            {
                chosen[row.Path] = row;
            }
        }

        return [.. paths.Select(path => chosen[path])];
    }

    /// <summary>The engine's record of one stored definition.</summary>
    public static AddonDefinition ToDefinition(AddonDefinitionDocument document) => new()
    {
        Id = document.Id,
        Entity = document.Entity,
        Path = document.Path,
        Kind = AddonDefinitionRules.ParseKind(document.Kind) ?? AddonKind.Object,
        Values = document.Values is { Count: > 0 } values ? [.. values.Select(value => new AddonValue(value.Value, value.Label))] : null,
        DisplayName = document.DisplayName,
        Description = document.Description,
        Retired = document.Retired,
    };
}

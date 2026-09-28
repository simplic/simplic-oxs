using OxQL.Model;
using OxQL.Model.Addon;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// The query engine's per-request view of an organisation's addon definitions, read from the
/// repository through <see cref="AddonDefinitionCache"/>. An entity's definitions are the
/// union of the rows stored under its current id and under every id it retired, so a service
/// that renames an entity keeps its definitions. Retired definitions are included, as the
/// engine's contract asks; a stored kind the engine does not know is read as
/// <see cref="AddonKind.Object"/>, an untyped container.
/// </summary>
public sealed class AddonDefinitionSource(IAddonDefinitionRepository repository, AddonDefinitionCache cache, OxSchemaRegistry schema) : IAddonDefinitionSource
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<AddonDefinition>> ForEntityAsync(string entity, Guid organisation, CancellationToken cancellationToken)
    {
        var current = CurrentId(schema.Model, entity);

        if (cache.TryGet(organisation, current, out var cached))
            return cached;

        var documents = await ReadAsync(repository, schema.Model, current, organisation);
        var definitions = documents.Select(document => ToDefinition(document) with { Entity = current }).ToList();

        cache.Set(organisation, current, definitions);

        return definitions;
    }

    /// <summary>
    /// The current id of <paramref name="entity"/>: itself when it is a live entity or unknown,
    /// the entity that replaced it when it is a retired id.
    /// </summary>
    public static string CurrentId(EntityModel model, string entity) =>
        !string.IsNullOrWhiteSpace(entity) && model.TryResolve(entity, out var definition, out _) ? definition.Id : entity;

    /// <summary>
    /// Every stored definition of an entity: the rows under its current id first, then the rows
    /// under each retired id in ordinal order. A path stored under more than one id is read from
    /// the first of them, so the current id wins over a retired one.
    /// </summary>
    /// <param name="repository">The definition repository.</param>
    /// <param name="model">The entity model that knows the retired ids.</param>
    /// <param name="entity">The entity's current or retired id.</param>
    /// <param name="organisation">The organisation, or null for the current request's.</param>
    public static async Task<IReadOnlyList<AddonDefinitionDocument>> ReadAsync(IAddonDefinitionRepository repository, EntityModel model, string entity, Guid? organisation)
    {
        IReadOnlyList<string> ids = model.TryResolve(entity, out var definition, out _)
            ? [definition.Id, .. definition.RetiredIds]
            : [entity];

        var union = new List<AddonDefinitionDocument>();
        var paths = new HashSet<string>(StringComparer.Ordinal);

        // Sequential on purpose: the ids are the handful a host declared, and the reads share
        // one repository and its session.
        foreach (var id in ids)
            foreach (var document in await repository.GetByEntityAsync(id, organisation))
                if (paths.Add(document.Path))
                    union.Add(document);

        return union;
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

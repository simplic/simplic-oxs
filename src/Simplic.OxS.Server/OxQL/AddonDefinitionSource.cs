using OxQL.Model.Addon;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// The query engine's per-request view of an organisation's addon definitions, read from the
/// repository through <see cref="AddonDefinitionCache"/>. Retired definitions are included,
/// as the engine's contract asks; a stored kind the engine does not know is read as
/// <see cref="AddonKind.Object"/>, an untyped container.
/// </summary>
public sealed class AddonDefinitionSource(IAddonDefinitionRepository repository, AddonDefinitionCache cache) : IAddonDefinitionSource
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<AddonDefinition>> ForEntityAsync(string entity, Guid organisation, CancellationToken cancellationToken)
    {
        if (cache.TryGet(organisation, entity, out var cached))
            return cached;

        var documents = await repository.GetByEntityAsync(entity, organisation);
        var definitions = documents.Select(ToDefinition).ToList();

        cache.Set(organisation, entity, definitions);

        return definitions;
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

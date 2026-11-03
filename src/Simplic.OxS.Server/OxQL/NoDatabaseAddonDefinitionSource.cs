using OxQL.Model.Addon;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// The addon definitions of a host that is configured with no database (no <c>MongoDB</c>
/// section): none, for the query engine, and <c>GET /schema/addons</c> answers that the host
/// holds none (404) where it finds this source. It needs nothing of a database to be created,
/// so the routes that are handed a source without reading it are constructed on such a host.
/// </summary>
internal sealed class NoDatabaseAddonDefinitionSource : IAddonDefinitionSource
{
    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<AddonDefinition>> ForEntityAsync(string entity, Guid organisation, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<AddonDefinition>>([]);
}

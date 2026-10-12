using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Repository interface for <see cref="AddonDefinitionDocument"/> persistence. The store is the
/// service's own (one collection per service) and keeps one row per organisation, entity id and
/// path: committing a write that would store a second one throws
/// <see cref="AddonDefinitionConflictException"/>.
/// </summary>
public interface IAddonDefinitionRepository : IOrganizationRepository<Guid, AddonDefinitionDocument, AddonDefinitionFilter>
{
    /// <summary>
    /// Retrieves every definition stored under any of <paramref name="entities"/> within one
    /// organisation, retired ones included, in one read: an entity's current id and the ids it
    /// retired are read together, never one read per id.
    /// </summary>
    /// <param name="entities">The entity ids (e.g. "logistics.shipment"); none reads nothing.</param>
    /// <param name="organizationId">The organisation, or null for the current request's.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The definitions, in no particular order.</returns>
    Task<IEnumerable<AddonDefinitionDocument>> GetByEntitiesAsync(IReadOnlyCollection<string> entities, Guid? organizationId = null, CancellationToken cancellationToken = default);
}

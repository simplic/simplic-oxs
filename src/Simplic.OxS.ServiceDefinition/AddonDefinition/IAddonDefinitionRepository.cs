using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Repository interface for <see cref="AddonDefinitionDocument"/> persistence.
/// </summary>
public interface IAddonDefinitionRepository : IOrganizationRepository<Guid, AddonDefinitionDocument, AddonDefinitionFilter>
{
    /// <summary>
    /// Retrieves every definition of one entity within one organisation, retired ones included.
    /// </summary>
    /// <param name="entity">The entity id (e.g. "logistics.shipment").</param>
    /// <param name="organizationId">The organisation, or null for the current request's.</param>
    /// <returns>The definitions, in no particular order.</returns>
    Task<IEnumerable<AddonDefinitionDocument>> GetByEntityAsync(string entity, Guid? organizationId = null);
}

using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Repository interface for <see cref="AddonField"/> persistence.
/// </summary>
public interface IAddonFieldRepository : IOrganizationRepository<Guid, AddonField, AddonFieldFilter>
{
    /// <summary>
    /// Retrieves all addon fields for the current organization.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of addon fields.</returns>
    Task<IEnumerable<AddonField>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves all addon fields for a specific object name within the current organization.
    /// </summary>
    /// <param name="objectName">The object name (e.g. "logistics.shipment").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of addon fields for the given object name.</returns>
    Task<IEnumerable<AddonField>> GetByObjectNameAsync(string objectName, CancellationToken ct = default);
}

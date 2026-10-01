using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Repository interface for endpoint contract persistence
/// </summary>
public interface IEndpointContractRepository : IOrganizationRepository<Guid, EndpointContract, EndpointContractFilter>
{
    /// <summary>
    /// Retrieves all organization settings.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of organization settings.</returns>
    Task<IEnumerable<EndpointContract>> GetAllAsync(CancellationToken ct = default);
}
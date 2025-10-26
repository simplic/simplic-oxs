namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Represents the contract information of a service.
/// </summary>
public class ServiceContract
{
    /// <summary>
    /// Gets or sets a list of provided endpoint contracts
    /// </summary>
    public IList<EndpointContractDefinition> EndpointContracts { get; set; } = [];

    /// <summary>
    /// Gets or sets a list of provided endpoint contracts
    /// </summary>
    public IList<RequiredEndpointContractDefinition> RequiredEndpointContracts { get; set; } = [];
}

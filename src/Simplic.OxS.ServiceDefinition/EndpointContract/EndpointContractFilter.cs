using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Filter for setting objects
/// </summary>
public class EndpointContractFilter : OrganizationFilterBase
{
    /// <summary>
    /// Gets or sets the internal name of the contract.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the provider name
    /// </summary>
    public string ProviderName { get; set; }
}
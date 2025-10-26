namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Represents the definition of an endpoint contract.
/// </summary>
public class RequiredEndpointContractDefinition
{
    /// <summary>
    /// Gets or sets the name of the contract. Names must be globally unique.
    /// E.g. geo.resolve-geolocation
    /// </summary>
    public string Name { get; set; }
}

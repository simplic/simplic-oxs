using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Represents an endpoint contract
/// </summary>
public class EndpointContract : OrganizationDocumentBase
{
    /// <summary>
    /// Gets or sets the contract name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the endpoint
    /// </summary>
    public string Endpoint { get; set; }
}

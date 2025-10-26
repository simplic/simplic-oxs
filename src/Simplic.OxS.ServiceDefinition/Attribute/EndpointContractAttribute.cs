using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Attribute to register a provided contract endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class EndpointContractAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the ProvidedEndpointContractAttribute class with the specified contract name.
    /// </summary>
    /// <param name="contractName">The name of the required endpoint contract. Cannot be null or empty.</param>
    /// <param name="endpoint">Endpoint path</param>
    public EndpointContractAttribute([NotNull] string contractName, [NotNull] string endpoint)
    {
        ContractName = contractName;
        Endpoint = endpoint;
    }

    /// <summary>
    /// Gets or sets the endpoint contract name.
    /// </summary>
    public string ContractName { get; set; }

    /// <summary>
    /// Gets or sets the endpoint path
    /// </summary>
    public string Endpoint { get; set; }
}

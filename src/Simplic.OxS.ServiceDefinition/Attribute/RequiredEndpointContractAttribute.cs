using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Attribute to register a required contract endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class RequiredEndpointContractAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the RequiredEndpointContractAttribute class with the specified contract name.
    /// </summary>
    /// <param name="contractName">The name of the required endpoint contract. Cannot be null or empty.</param>
    /// <param name="providerName">Endpoint path</param>
    /// <param name="allowMultiple">Allow multiple registrations</param>
    public RequiredEndpointContractAttribute([NotNull] string contractName, [NotNull] string providerName, bool allowMultiple)
    {
        ContractName = contractName;
        ProviderName = providerName;
        AllowMultiple = allowMultiple;
    }

    /// <summary>
    /// Gets or sets the endpoint contract name.
    /// </summary>
    public string ContractName { get; set; }

    /// <summary>
    /// Gets or sets the provider name
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets whether multiple provider can be set
    /// </summary>
    public bool AllowMultiple { get; set; }
}

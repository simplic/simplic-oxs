using Simplic.OxS.ServiceDefinition;
using System.Reflection;

namespace Simplic.OxS.Server.Service;

/// <summary>
/// Service definition
/// </summary>
public class ServiceDefinitionService
{
    private static ServiceObject serviceDefinition;

    /// <summary>
    /// Fill service definition
    /// </summary>
    public void Fill()
    {
        if (serviceDefinition != null)
            ServiceObject = serviceDefinition;

        serviceDefinition = new ServiceObject
        {
            Name = ServiceName,
            Version = Version,
            Type = "internal",
            BaseUrl = $"/{ServiceName}-api/{Version}/",
            SwaggerJsonUrl = $"/{ServiceName}-api/{Version}/swagger.json",
            ModelDefinitionUrl = $"/{ServiceName}-api/{Version}/modeldefinition"
        };

        var allContracts = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => GetAllContractNames(a))
            .Distinct();

        var allRequiredContracts = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => GetAllRequiredContractNames(a))
            .Distinct();

        foreach (var contractName in allContracts)
            ServiceObject.Contract.EndpointContracts.Add(new EndpointContract { Name = contractName });

        foreach (var contractName in allRequiredContracts)
            ServiceObject.Contract.RequiredEndpointContracts.Add(new EndpointContract { Name = contractName });

        // Cache service definition
        ServiceObject = serviceDefinition;
    }

    /// <summary>
    /// Gets or sets the service name
    /// </summary>
    public string ServiceName { get; set; }

    /// <summary>
    /// Gets or sets the service version
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the service object
    /// </summary>
    public ServiceObject ServiceObject { get; set; }

    private static IEnumerable<string> GetAllContractNames(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetCustomAttributes<EndpointContractAttribute>())
            .Select(attr => attr.ContractName)
            .Distinct();
    }

    private static IEnumerable<string> GetAllRequiredContractNames(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetCustomAttributes<RequiredEndpointContractAttribute>())
            .Select(attr => attr.ContractName)
            .Distinct();
    }
}

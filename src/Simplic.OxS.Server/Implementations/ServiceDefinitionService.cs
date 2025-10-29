using Microsoft.AspNetCore.Mvc.TagHelpers;
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

        foreach (var attr in allContracts)
            serviceDefinition.Contract.EndpointContracts.Add(new EndpointContractDefinition { Name = attr.Attribute.ContractName, Endpoint = attr.Attribute.Endpoint, Schema = attr.Schema });

        foreach (var contractName in allRequiredContracts)
            serviceDefinition.Contract.RequiredEndpointContracts.Add(new RequiredEndpointContractDefinition { Name = contractName });

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

    record _ContractResult (EndpointContractAttribute Attribute, JsonSchema Schema);

    private static IEnumerable<_ContractResult> GetAllContractNames(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(method => method.GetCustomAttributes<EndpointContractAttribute>(),
                (method, attribute) => new _ContractResult(attribute, SchemaGenerator.GenerateMethodJsonSchema(method)));
    }

    private static IEnumerable<string> GetAllRequiredContractNames(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t =>
                t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .SelectMany(m => m.GetCustomAttributes<RequiredEndpointContractAttribute>())
                .Select(attr => attr.ContractName)
            )
            .Distinct();
    }
}

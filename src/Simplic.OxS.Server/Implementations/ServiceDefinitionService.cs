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

        foreach (var providerGroup in allContracts.GroupBy(x => x.Attribute.ProviderName))
        {
            var contract = new ServiceContract
            {
                ProviderName = providerGroup.Key,
                EndpointContracts = providerGroup.Select(x => new EndpointContractDefinition
                {
                    Name = x.Attribute.ContractName,
                    Endpoint = x.Attribute.Endpoint
                }).ToList()
            };

            serviceDefinition.Contracts.Add(contract);
        }

        foreach (var providerGroup in allRequiredContracts.GroupBy(x => x.Attribute.ProviderName))
        {
            ServiceContract contract = serviceDefinition.Contracts.FirstOrDefault(x => x.ProviderName == providerGroup.Key);

            if (contract == null)
            {
                contract = new ServiceContract
                {
                    ProviderName = providerGroup.Key
                };

                serviceDefinition.Contracts.Add(contract);
            }

            contract.RequiredEndpointContracts = providerGroup.Select(x => new RequiredEndpointContractDefinition
            {
                Name = x.Attribute.ContractName,
                AllowMultiple = x.AllowMultiple
            }).ToList();
        }

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

    record _ContractResult(EndpointContractAttribute Attribute, bool AllowMultiple);

    private static IEnumerable<_ContractResult> GetAllContractNames(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(method => method.GetCustomAttributes<EndpointContractAttribute>(),
                (method, attribute) => new _ContractResult(attribute, false));
    }

    private static IEnumerable<_ContractResult> GetAllRequiredContractNames(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(method => method.GetCustomAttributes<RequiredEndpointContractAttribute>(),
                (method, attribute) => new _ContractResult(new EndpointContractAttribute(attribute.ContractName, "", attribute.ProviderName), attribute.AllowMultiple));
    }
}

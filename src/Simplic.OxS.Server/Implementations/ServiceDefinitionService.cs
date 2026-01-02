using DnsClient.Internal;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.ServiceDefinition;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Service;

/// <summary>
/// Service definition
/// </summary>
public class ServiceDefinitionService(IServiceProvider serviceProvider, ILogger<ServiceDefinitionService> logger)
{
    private static ServiceObject serviceDefinition;

    /// <summary>
    /// Fill service definition
    /// </summary>
    public async Task Fill()
    {
        if (serviceDefinition != null)
            ServiceObject = serviceDefinition;

        serviceDefinition = new ServiceObject
        {
            Name = ServiceName.ToLower(),
            Version = Version.ToLower(),
            Type = "internal",
            BaseUrl = $"/{ServiceName}-api/{Version}/".ToLower(),
            SwaggerJsonUrl = $"/{ServiceName}-api/{Version}/swagger/{Version}/swagger.json".ToLower(),
            ModelDefinitionUrl = $"/{ServiceName}-api/{Version}/modeldefinition".ToLower()
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

        // Load gRPC definitions from proto files
        LoadGrpcDefinitions();

        await ExportGraphQlSchema(serviceDefinition);

        // Cache service definition
        ServiceObject = serviceDefinition;
    }

    /// <summary>
    /// Exports the GraphQL schema and adds it to the service object
    /// </summary>
    /// <param name="serviceObject"></param>
    /// <returns></returns>
    private async Task ExportGraphQlSchema(ServiceObject serviceObject)
    {
        try
        {
            var requestExecutorResolver = serviceProvider.GetRequiredService<IRequestExecutorResolver>();

            var executor = await requestExecutorResolver.GetRequestExecutorAsync();
            var schema = executor.Schema;

            serviceObject.GraphQLSchema = schema.Print();
        }
        catch(Exception ex)
        {
            logger.LogWarning(ex, "Could not get GraphQL schema");
        }
    }

    /// <summary>
    /// Loads gRPC definitions from proto files found at runtime
    /// </summary>
    private void LoadGrpcDefinitions()
    {
        try
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var protoFiles = Directory.GetFiles(baseDirectory, "*.proto", SearchOption.AllDirectories);

            Console.WriteLine($"Found {protoFiles.Length} proto file(s) in {baseDirectory}");

            foreach (var protoFile in protoFiles)
            {
                try
                {
                    Console.WriteLine($"Processing proto file: {protoFile}");
                    var grpcDefinition = CreateGrpcDefinitionFromProtoFile(protoFile);
                    if (grpcDefinition != null)
                    {
                        serviceDefinition.GrpcDefinitions ??= new List<GrpcDefinitions>();
                        serviceDefinition.GrpcDefinitions.Add(grpcDefinition);
                        Console.WriteLine($"Added gRPC definition - Package: {grpcDefinition.Package}, Service: {grpcDefinition.Service}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing proto file {protoFile}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning for proto files: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a GrpcDefinitions instance from a proto file
    /// </summary>
    /// <param name="protoFilePath">Path to the proto file</param>
    /// <returns>GrpcDefinitions instance or null if parsing failed</returns>
    private GrpcDefinitions? CreateGrpcDefinitionFromProtoFile(string protoFilePath)
    {
        try
        {
            var protoContent = File.ReadAllText(protoFilePath);
            var protoBytes = File.ReadAllBytes(protoFilePath);

            // Extract package name using regex
            var packageMatch = Regex.Match(protoContent, @"package\s+([^;]+);", RegexOptions.IgnoreCase);
            var packageName = packageMatch.Success ? packageMatch.Groups[1].Value.Trim() : "";

            // Extract service names using regex
            var serviceMatches = Regex.Matches(protoContent, @"service\s+(\w+)\s*\{", RegexOptions.IgnoreCase);

            if (serviceMatches.Count == 0)
            {
                Console.WriteLine($"No services found in proto file: {protoFilePath}");
                return null;
            }

            // For now, we'll take the first service found
            // In a more complex scenario, you might want to create separate GrpcDefinitions for each service
            var serviceName = serviceMatches[0].Groups[1].Value;

            // Combine package and service name for the full service name
            var fullServiceName = !string.IsNullOrEmpty(packageName) ? $"{packageName}.{serviceName}" : serviceName;

            return new GrpcDefinitions
            {
                Package = packageName,
                Service = fullServiceName,
                ProtoFile = protoBytes
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing proto file {protoFilePath}: {ex.Message}");
            return null;
        }
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
                (method, attribute) => new _ContractResult(new EndpointContractAttribute(attribute.ContractName, "", ""), attribute.AllowMultiple));
    }
}

using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Attribute to register a gRPC endpoint contract
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class GrpcEndpointContractAttribute : EndpointContractAttribute
{
    /// <summary>
    /// Initializes a new instance of the GrpcEndpointContractAttribute class with the specified contract name.
    /// </summary>
    /// <param name="contractName">Name of the contract</param>
    /// <param name="serviceName">Microservice / service name within the k8s network, for example simplic-oxs-document</param>
    /// <param name="grpcPackage">gRPC package name</param>
    /// <param name="grpcService">gRPC service name</param>
    /// <param name="grpcMethod">gRPC method name</param>
    /// <param name="providerName">Provider name (if multiple provider are supported for the given contract)</param>
    public GrpcEndpointContractAttribute([NotNull] string contractName, [NotNull] string serviceName, [NotNull] string grpcPackage, [NotNull] string grpcService, [NotNull] string grpcMethod, [NotNull] string providerName)
        : base(contractName, $"[gprc]http://{serviceName}:8082::{grpcPackage}.{grpcService}::{grpcMethod}", providerName)
    {

    }
}

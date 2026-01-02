using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Simplic.OxS.ServiceDefinition;
using Simplic.OxS.Settings;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Simplic.OxS.Server.Extensions;

/// <summary>
/// Extension for managing remove services
/// </summary>
public static class RemoteServiceExtension
{
    /// <summary>
    /// Add remove service system
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddRemoteService(this IServiceCollection services)
    {
        services.AddTransient<IRemoteServiceInvoker, RemoteServiceInvoker>();

        return services;
    }
}

/// <summary>
/// Provides functionality to invoke remote service endpoints using contract-based routing, supporting both HTTP and
/// gRPC protocols for asynchronous calls.
/// </summary>
/// <remarks>RemoteServiceInvoker is intended for internal use in scenarios where service endpoints are
/// dynamically resolved and invoked based on contract names. Endpoint URLs are cached for performance, and cache
/// refreshes are handled automatically. The invoker supports both HTTP and gRPC protocols, selecting the appropriate
/// transport based on endpoint configuration. All remote calls include organization and user metadata for authorization
/// and auditing purposes.</remarks>
/// <param name="distributedCache">The distributed cache used to store and retrieve endpoint URLs for contracts, enabling efficient endpoint resolution
/// and cache refresh.</param>
/// <param name="endpointContractRepository">The repository used to query contract-to-endpoint mappings for the current organization when cache misses occur.</param>
/// <param name="requestContext">The context containing organization and user identifiers, used for endpoint resolution and for passing metadata to
/// remote service calls.</param>
internal class RemoteServiceInvoker(IDistributedCache distributedCache
                                  , IEndpointContractRepository endpointContractRepository
                                  , IOptions<AuthSettings> settings
                                  , IRequestContext requestContext) : IRemoteServiceInvoker
{
    /// <inheritdoc />
    public async Task<T?> Call<T, P>([NotNull] string contractOrUri, string? provider, P parameter, Func<P, Task<T>>? defaultImpl = null)
            where T : class, IMessage<T>, new()
            where P : class, IMessage<P>, new()

    {
        // functionName: simplic.ox.routing.calculate

        if (string.IsNullOrWhiteSpace(contractOrUri))
            throw new Exception("No contract passed for remote service call.");

        var uri = "";

        // Allow to pass contract or uri
        if (contractOrUri.StartsWith("["))
            uri = contractOrUri;
        else
            uri = await GetEndpointAsync(contractOrUri, provider);

        if (!string.IsNullOrWhiteSpace(uri))
        {
            if (TryParseProtocol(uri, out string? protocol, out string? url))
            {
                if (protocol == "grpc")
                {
                    return await RemoteGrpcCall<T, P>(settings, requestContext, parameter, url);
                }
                else
                {
                    throw new Exception($"Invalid protocol: `{protocol}`");
                }
            }
            else
                throw new Exception($"Could not parse url: {uri}");
        }

        if (defaultImpl != null)
            return await defaultImpl(parameter);

        return default;
    }

    private static async Task<T?> RemoteGrpcCall<T, P>(IOptions<AuthSettings> settings, IRequestContext requestContext, P parameter, [NotNull] string url)
        where T : class, IMessage<T>, new()
        where P : class, IMessage<P>, new()
    {
        var splittedUrl = url.Split("::", StringSplitOptions.RemoveEmptyEntries);
        if (splittedUrl.Length != 3)
            throw new Exception("Grpc url must consists of 3 parts <address>::<service>::method");

        var address = splittedUrl[0];
        var serviceName = splittedUrl[1];
        var methodName = splittedUrl[2];
        GrpcChannel channel = GrpcChannel.ForAddress(address);
        var invoker = channel.CreateCallInvoker();

        // 2.  Build marshallers & a Method description on the fly
        var method = new Method<P, T>(
            MethodType.Unary,
            serviceName,
            methodName,
            CreateMarshaller<P>(),
            CreateMarshaller<T>()
        );

        // Add call metadata for passing organization-id and user-id
        var headers = new Metadata
                {
                    { Constants.HttpHeaderOrganizationIdKey, requestContext.OrganizationId.Value.ToString() },
                    { Constants.HttpHeaderUserIdKey, requestContext.UserId.Value.ToString() },
                    { Constants.HttpAuthorizationSchemeInternalKey, settings.Value.InternalApiKey }
                };

        var options = new CallOptions(headers);

        // 4.  Fire the RPC and await the protobuf reply
        return await invoker
            .AsyncUnaryCall(method, host: null, options, parameter)
            .ResponseAsync.ConfigureAwait(false);
    }

    private static Marshaller<T> CreateMarshaller<T>()
    where T : class, IMessage<T>, new() =>
    Marshallers.Create(
        (T msg) => msg.ToByteArray(),
        data =>
        {
            var m = new T();
            m.MergeFrom(data);
            return m;
        });

    /// <summary>
    /// Asynchronously retrieves the endpoint URL associated with the specified contract for the current organization.
    /// </summary>
    /// <remarks>If the endpoint is cached, the cache is refreshed in the background and the cached value is
    /// returned. If not cached, the endpoint is retrieved from the repository and cached for 10 minutes. This method is
    /// thread-safe and intended for use in asynchronous workflows.</remarks>
    /// <param name="contract">The name of the contract for which to retrieve the endpoint. Cannot be null or empty.</param>
    /// <param name="provider">An optional provider identifier to further specify the endpoint. Can be null.</param>
    /// <returns>A string containing the endpoint URL if found; otherwise, null.</returns>
    private async Task<string?> GetEndpointAsync(string contract, string? provider)
    {
        var key = $"{requestContext.OrganizationId.Value}_{contract}_{provider ?? ""}";

        var value = await distributedCache.GetStringAsync(key);

        if (!string.IsNullOrWhiteSpace(value))
        {
            // Refresh cache in the background
            _ = distributedCache.RefreshAsync(key);

            return value;
        }

        var endpointContracts = (await endpointContractRepository.GetByFilterAsync(new EndpointContractFilter
        {
            QueryAllOrganizations = false,
            OrganizationId = requestContext.OrganizationId.Value,
            Name = contract,
            IsDeleted = false
        })).ToList();
        var endpointContract = endpointContracts.FirstOrDefault(x => (x.ProviderName ?? "") == "");

        // Select by provider, if one is required
        if (!string.IsNullOrWhiteSpace(provider))
            endpointContract = endpointContracts.FirstOrDefault(ec => ec.ProviderName == provider);

        if (endpointContract == null)
            return null;

        // Cache for 10 minutes
        _ = distributedCache.SetStringAsync(key, endpointContract.Endpoint, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });

        return endpointContract.Endpoint;
    }

    /// <summary>
    /// Attempts to extract the protocol and URL from the specified URI string if it matches a supported protocol
    /// format.
    /// </summary>
    /// <remarks>Supported protocol prefixes are "[grpc]" and "[http.post]". If the URI does not start with a
    /// supported prefix, both out parameters are set to null and the method returns false.</remarks>
    /// <param name="uri">The URI string to parse. Must begin with a supported protocol prefix such as "[grpc]" or "[http.post]".</param>
    /// <param name="protocol">When this method returns, contains the protocol name extracted from the URI if parsing succeeds; otherwise,
    /// null.</param>
    /// <param name="url">When this method returns, contains the URL portion of the URI with the protocol prefix removed if parsing
    /// succeeds; otherwise, null.</param>
    /// <returns>true if the URI contains a recognized protocol prefix and parsing succeeds; otherwise, false.</returns>
    private bool TryParseProtocol(string uri, out string? protocol, out string? url)
    {
        if (uri.StartsWith("[grpc]"))
        {
            url = uri.Remove(0, 6).Trim();
            protocol = "grpc";

            return true;
        }

        url = null;
        protocol = null;

        return false;
    }
}

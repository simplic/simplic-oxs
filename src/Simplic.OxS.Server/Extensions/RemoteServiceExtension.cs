using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
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
        services.AddTransient<IRemoveServiceInvoker, RemoveServiceInvoker>();

        return services;
    }
}

internal class RemoveServiceInvoker(IDistributedCache distributedCache, IRequestContext requestContext) : IRemoveServiceInvoker
{
    /// <inheritdoc />
    public async Task<T?> Call<T, P>([NotNull] string functionName, P parameter, string? defaultTargetUri)
            where T : class, IMessage<T>, new()
            where P : class, IMessage<P>, new()

    {
        // functionName: simplic.ox.routing.calculate

        if (string.IsNullOrWhiteSpace(functionName))
            throw new Exception("No function name passed for remove service call.");

        var uri = await GetFunctionUriAsync(functionName, defaultTargetUri);

        if (TryParseProtocol(uri, out string? protocol, out string? url))
        {
            if (protocol == "http.post")
            {
                return await RemoteHttpCall<T, P>(requestContext, parameter, url);
            }
            else if (protocol == "grpc")
            {
                return await RemoteGrpcCall<T, P>(requestContext, parameter, url);
            }
            else
            {
                throw new Exception($"Invalid protocol: `{protocol}`");
            }
        }

        return default(T);
    }

    private static async Task<T?> RemoteGrpcCall<T, P>(IRequestContext requestContext, P parameter, string? url)
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
                    { Constants.HttpHeaderUserIdKey, requestContext.UserId.Value.ToString() }
                };

        var options = new CallOptions(headers);

        // 4.  Fire the RPC and await the protobuf reply
        return await invoker
            .AsyncUnaryCall(method, host: null, options, parameter)
            .ResponseAsync.ConfigureAwait(false);
    }

    private static async Task<T?> RemoteHttpCall<T, P>(IRequestContext requestContext, P parameter, string? url)
        where T : class, IMessage<T>, new()
        where P : class, IMessage<P>, new()
    {

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add(Constants.HttpHeaderOrganizationIdKey, requestContext.OrganizationId.Value.ToString());
        httpClient.DefaultRequestHeaders.Add(Constants.HttpHeaderUserIdKey, requestContext.UserId.Value.ToString());

        string json = JsonSerializer.Serialize(parameter);

        var response = await httpClient.PostAsync(new Uri(url), new StringContent(json, Encoding.UTF8, "application/json"));

        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<T>();
        else
            throw new Exception($"Could not call internal function: {await response.Content.ReadAsStringAsync()}");
    }

    /// <inheritdoc />
    public async Task<T> Call<T, P>([NotNull] string functionName, P parameter, Func<T, P>? defaultImpl)
    {
        throw new NotImplementedException();
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

    private async Task<string?> GetFunctionUriAsync(string functionName, string? defaultUri)
    {
        var key = $"{requestContext.OrganizationId.Value}_{functionName}";

        var value = await distributedCache.GetStringAsync(key);

        if (!string.IsNullOrWhiteSpace(value))
        {
            // Refresh cache in the background
            _ = distributedCache.RefreshAsync(key);

            return value;
        }

        var uri = defaultUri;

        if (!string.IsNullOrWhiteSpace(uri))
        {
            await distributedCache.SetStringAsync(key, uri, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            });
        }

        return uri;
    }

    private bool TryParseProtocol(string uri, out string? protocol, out string? url)
    {
        if (uri.StartsWith("[grpc]"))
        {
            url = uri.Remove(0, 6).Trim();
            protocol = "grpc";

            return true;
        }

        if (uri.StartsWith("[http.post]"))
        {
            url = uri.Remove(0, 11).Trim();
            protocol = "http.post";

            return true;
        }

        url = null;
        protocol = null;

        return false;
    }
}

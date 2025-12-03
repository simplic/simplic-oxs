using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.Middleware;

/// <summary>
/// gRPC interceptor for host validation and security
/// </summary>
public class GrpcSecurityInterceptor(ILogger<GrpcSecurityInterceptor> logger
                                    , ICurrentService currentService) : Interceptor
{
    private static string[] allowedHosts = null;

    /// <summary>
    /// Intercept unary server calls for validation
    /// </summary>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
                                             TRequest request,
                                             ServerCallContext context,
                                             UnaryServerMethod<TRequest, TResponse> continuation)
    {
        if (!await ValidateHostAccess(context))
        {
            logger.LogWarning("gRPC call rejected from unauthorized host: {Host}", context.Host);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Host not authorized for gRPC access"));
        }

        return await continuation(request, context);
    }

    /// <summary>
    /// Intercept server streaming calls for validation
    /// </summary>
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
                                    TRequest request,
                                    IServerStreamWriter<TResponse> responseStream,
                                    ServerCallContext context,
                                    ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        if (!await ValidateHostAccess(context))
        {
            logger.LogWarning("gRPC streaming call rejected from unauthorized host: {Host}", context.Host);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Host not authorized for gRPC access"));
        }

        await continuation(request, responseStream, context);
    }

    /// <summary>
    /// Intercept client streaming calls for validation
    /// </summary>
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
                    IAsyncStreamReader<TRequest> requestStream,
                    ServerCallContext context,
                    ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        if (!await ValidateHostAccess(context))
        {
            logger.LogWarning("gRPC client streaming call rejected from unauthorized host: {Host}", context.Host);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Host not authorized for gRPC access"));
        }

        return await continuation(requestStream, context);
    }

    /// <summary>
    /// Intercept duplex streaming calls for validation
    /// </summary>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
              IAsyncStreamReader<TRequest> requestStream,
              IServerStreamWriter<TResponse> responseStream,
              ServerCallContext context,
              DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        if (!await ValidateHostAccess(context))
        {
            logger.LogWarning("gRPC duplex streaming call rejected from unauthorized host: {Host}", context.Host);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Host not authorized for gRPC access"));
        }

        await continuation(requestStream, responseStream, context);
    }

    /// <summary>
    /// Validate if the host is allowed for gRPC access
    /// </summary>
    private Task<bool> ValidateHostAccess(ServerCallContext context)
    {
        Console.WriteLine("Validate host access");

        // Extract host from context
        var host = ExtractHostFromContext(context);

        Console.WriteLine($"Host: {host}");

        if (string.IsNullOrWhiteSpace(host))
        {
            return Task.FromResult(false);
        }

        // Allow localhost variations
        if (IsLocalhost(host))
        {
            Console.WriteLine("Is local host");
            return Task.FromResult(true);
        }

        // Set list of allowed hosts, defined in bootstrap file
        allowedHosts ??= [$"{currentService.ServiceName}", $"{currentService.ServiceName}-{currentService.ApiVersion}", $"simplic-oxs-{currentService.ServiceName}-{currentService.ApiVersion}"];

        Console.WriteLine($"Allowed hosts: {string.Join(',', allowedHosts)}");

        // Validate against allowed hosts
        return Task.FromResult(allowedHosts.Contains(host));
    }

    /// <summary>
    /// Extract host from ServerCallContext
    /// </summary>
    private string? ExtractHostFromContext(ServerCallContext context)
    {
        // Try to get host from :authority header (HTTP/2)
        var authorityHeader = context.RequestHeaders.GetValue(":authority");
        if (!string.IsNullOrWhiteSpace(authorityHeader))
        {
            return authorityHeader.Split(':')[0]; // Remove port if present
        }

        // Try to get host from Host header
        var hostHeader = context.RequestHeaders.GetValue("host");
        if (!string.IsNullOrWhiteSpace(hostHeader))
        {
            return hostHeader.Split(':')[0]; // Remove port if present
        }

        // Fallback to peer (might be IP address)
        return context.Peer;
    }

    /// <summary>
    /// Check if the host is localhost or local IP
    /// </summary>
    private static bool IsLocalhost(string host)
    {
        string[] localhostVariants = ["localhost", "127.0.0.1", "::1", "0.0.0.0"];

        return localhostVariants.Any(localhost => host.Contains(localhost, StringComparison.OrdinalIgnoreCase));
    }
}
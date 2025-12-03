using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Middleware;

/// <summary>
/// gRPC interceptor for host validation and security
/// </summary>
/// <param name="logger"></param>
/// <param name="settings"></param>
public class GrpcSecurityInterceptor(ILogger<GrpcSecurityInterceptor> logger
                                   , IOptions<AuthSettings> settings) : Interceptor
{
    private static string? iApiKey = null;

    /// <summary>
    /// Intercept unary server calls for validation
    /// </summary>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
                                             TRequest request,
                                             ServerCallContext context,
                                             UnaryServerMethod<TRequest, TResponse> continuation)
    {
        if (!await ValidateApiKey(context))
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
        if (!await ValidateApiKey(context))
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
        if (!await ValidateApiKey(context))
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
        if (!await ValidateApiKey(context))
        {
            logger.LogWarning("gRPC duplex streaming call rejected from unauthorized host: {Host}", context.Host);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Host not authorized for gRPC access"));
        }

        await continuation(requestStream, responseStream, context);
    }

    /// <summary>
    /// Validate the internal API key from the gRPC context
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    private Task<bool> ValidateApiKey(ServerCallContext context)
    {
        if (iApiKey == null)
            iApiKey = settings.Value.InternalApiKey;

        var internalApiKey = context.RequestHeaders.Get(Constants.HttpAuthorizationSchemeInternalKey)?.Value ?? "<null-i-api-key>";
        
        return Task.FromResult(internalApiKey == iApiKey);
    }
}
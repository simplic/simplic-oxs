using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server;

/// <summary>
/// Intercepts incoming gRPC server calls to extract request header information and populate the request context for
/// downstream processing.
/// </summary>
/// <remarks>This interceptor extracts user, organization, and correlation identifiers from gRPC request headers
/// and sets them in the request context, making them available to other services within the request scope. If a
/// correlation ID is not provided, a new one is generated automatically. This enables consistent tracking and logging
/// of requests across distributed systems.</remarks>
/// <param name="scopeFactory">The factory used to create service scopes for resolving scoped dependencies during request processing.</param>
/// <param name="logger">The logger used to record informational and error messages related to header processing and request context
/// population.</param>
public class GrpcHeaderContextInterceptor(IServiceScopeFactory scopeFactory, ILogger<GrpcHeaderContextInterceptor> logger) : Interceptor
{
    /// <summary>
    /// Handles a unary gRPC server call by processing request headers and invoking the specified continuation delegate.
    /// </summary>
    /// <remarks>This method is typically used to implement middleware or interceptors in a gRPC server
    /// pipeline. It allows custom processing of request headers before passing control to the next handler.</remarks>
    /// <typeparam name="TRequest">The type of the request message received from the client.</typeparam>
    /// <typeparam name="TResponse">The type of the response message returned to the client.</typeparam>
    /// <param name="request">The request message sent by the client.</param>
    /// <param name="context">The context for the current server call, providing metadata and control over the call's lifecycle.</param>
    /// <param name="continuation">A delegate that processes the request and returns a response asynchronously. This is typically the next handler
    /// in the call pipeline.</param>
    /// <returns>A task representing the asynchronous operation, containing the response message to be sent to the client.</returns>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ProcessHeaders(context);
        return await continuation(request, context);
    }

    /// <summary>
    /// Handles a server streaming gRPC server call by processing request headers and invoking the specified continuation delegate.
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    /// <param name="request"></param>
    /// <param name="responseStream"></param>
    /// <param name="context"></param>
    /// <param name="continuation"></param>
    /// <returns></returns>
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
                                TRequest request,
                                IServerStreamWriter<TResponse> responseStream,
                                ServerCallContext context,
                                ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ProcessHeaders(context);
        await continuation(request, responseStream, context);
    }

    /// <summary>
    /// Handles a client streaming gRPC call by processing request headers and invoking the specified continuation
    /// method.
    /// </summary>
    /// <remarks>This method is typically used to intercept or augment client streaming calls in a gRPC server
    /// implementation. It processes incoming headers before delegating the call to the provided continuation. Thread
    /// safety and call context management should be considered when overriding this method.</remarks>
    /// <typeparam name="TRequest">The type of messages received from the client stream.</typeparam>
    /// <typeparam name="TResponse">The type of response returned to the client after processing the stream.</typeparam>
    /// <param name="requestStream">An asynchronous stream reader used to receive messages from the client.</param>
    /// <param name="context">The context for the current server call, providing metadata and control over the call lifecycle.</param>
    /// <param name="continuation">A delegate that processes the client stream and returns a response to the client.</param>
    /// <returns>A task representing the asynchronous operation, containing the response to be sent to the client.</returns>
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
                                        IAsyncStreamReader<TRequest> requestStream,
                                        ServerCallContext context,
                                        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ProcessHeaders(context);
        return await continuation(requestStream, context);
    }

    /// <summary>
    /// Handles a duplex streaming gRPC server call by processing request headers and invoking the specified
    /// continuation method.
    /// </summary>
    /// <remarks>This method is typically used to intercept or augment duplex streaming server calls in gRPC
    /// services. It processes incoming headers before delegating to the continuation handler.</remarks>
    /// <typeparam name="TRequest">The type of messages received from the client in the request stream.</typeparam>
    /// <typeparam name="TResponse">The type of messages sent to the client in the response stream.</typeparam>
    /// <param name="requestStream">An asynchronous stream reader used to receive messages from the client.</param>
    /// <param name="responseStream">An asynchronous stream writer used to send messages to the client.</param>
    /// <param name="context">The context for the current server call, providing metadata and control over the call lifecycle.</param>
    /// <param name="continuation">A delegate representing the next handler in the call pipeline, which processes the duplex streaming operation.</param>
    /// <returns>A task that represents the asynchronous operation of handling the duplex streaming call.</returns>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
                                        IAsyncStreamReader<TRequest> requestStream,
                                        IServerStreamWriter<TResponse> responseStream,
                                        ServerCallContext context,
                                        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ProcessHeaders(context);
        await continuation(requestStream, responseStream, context);
    }

    /// <summary>
    /// Processes incoming gRPC request headers to extract user, organization, and correlation identifiers from the
    /// request context.
    /// </summary>
    /// <remarks>If the correlation identifier header is missing or invalid, a new correlation ID is
    /// generated. The extracted identifiers are assigned to the current request context, if available. This method logs
    /// the processed identifiers for diagnostic purposes.</remarks>
    /// <param name="context">The server call context containing the request headers to be processed.</param>
    private void ProcessHeaders(ServerCallContext context)
    {
        try
        {
            // Extract headers using constants from Constants.cs
            var metadata = context.RequestHeaders;
            var userIdHeader = metadata.GetValue(Constants.HttpHeaderUserIdKey);
            var organizationIdHeader = metadata.GetValue(Constants.HttpHeaderOrganizationIdKey);
            var correlationIdHeader = metadata.GetValue(Constants.HttpHeaderCorrelationIdKey);

            // Parse header values to GUIDs
            Guid? userId = null;
            if (!string.IsNullOrEmpty(userIdHeader) && Guid.TryParse(userIdHeader, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            Guid? organizationId = null;
            if (!string.IsNullOrEmpty(organizationIdHeader) && Guid.TryParse(organizationIdHeader, out var parsedOrganizationId))
            {
                organizationId = parsedOrganizationId;
            }

            Guid? correlationId = null;
            if (!string.IsNullOrEmpty(correlationIdHeader) && Guid.TryParse(correlationIdHeader, out var parsedCorrelationId))
            {
                correlationId = parsedCorrelationId;
            }
            else
            {
                // Generate a new correlation ID if none provided
                correlationId = Guid.NewGuid();
            }

            // Get the request context service from the current request scope
            if (context.GetHttpContext()?.RequestServices.GetService<IRequestContext>() is IRequestContext requestContext)
            {
                requestContext.UserId = userId;
                requestContext.OrganizationId = organizationId;
                requestContext.CorrelationId = correlationId;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing headers in GrpcHeaderContextInterceptor");
        }
    }
}
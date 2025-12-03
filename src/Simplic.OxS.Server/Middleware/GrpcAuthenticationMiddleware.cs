using Google.Protobuf.WellKnownTypes;
using MassTransit.Caching.Internals;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.Settings;
using StackExchange.Redis;
using System;

namespace Simplic.OxS.Server.Middleware;

/// <summary>
/// Middleware that enforces authentication for incoming gRPC requests by validating host access using an internal API
/// key.
/// </summary>
/// <remarks>In development environments, authentication checks for gRPC requests are bypassed to facilitate local
/// testing. In production, requests must include a valid internal API key in the header to be authorized. Unauthorized
/// requests receive a 403 Forbidden response.</remarks>
/// <param name="next">The next middleware delegate in the HTTP request pipeline.</param>
/// <param name="logger">The logger used to record authentication events and diagnostics.</param>
/// <param name="environment">The hosting environment, used to determine if the application is running in development mode.</param>
/// <param name="settings">The authentication settings containing the internal API key used for validation.</param>
public class GrpcAuthenticationMiddleware(RequestDelegate next
                                        , ILogger<GrpcAuthenticationMiddleware> logger
                                        , IWebHostEnvironment environment
                                        , IOptions<AuthSettings> settings)
{
    private static string? iApiKey = null;

    /// <summary>
    /// Processes an incoming HTTP request, applying gRPC-specific validation and authorization before invoking the next
    /// middleware in the pipeline.
    /// </summary>
    /// <remarks>For gRPC requests, the method enforces host authorization unless running in development mode.
    /// If authorization fails, the response status is set to 403 Forbidden and the request is not passed to subsequent
    /// middleware.</remarks>
    /// <param name="context">The HTTP context for the current request. Provides access to request and response information used for
    /// validation and authorization.</param>
    /// <returns>A task that represents the asynchronous operation of processing the HTTP request.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply validation to gRPC requests
        if (IsGrpcRequest(context))
        {
            // Skip validation in development if configured
            if (environment.IsDevelopment())
            {
                logger.LogDebug("gRPC request allowed in development mode from host: {Host}", context.Request.Host.Host);
                await next(context);
                return;
            }

            if (!CheckApiKey(context.Request))
            {
                logger.LogWarning($"gRPC request from unauthorized host: {context.Request.Host}");

                context.Response.StatusCode = 403; // Forbidden
                await context.Response.WriteAsync("Access denied: Host not allowed for gRPC requests");
                return;
            }
        }

        await next(context);
    }

    /// <summary>
    /// Check if the request is a gRPC request
    /// </summary>
    private static bool IsGrpcRequest(HttpContext context)
    {
        return context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true ||
                  (context.Request.Protocol == "HTTP/2" && context.Request.Headers.ContainsKey("grpc-accept-encoding"));
    }

    /// <summary>
    /// Determines whether the specified HTTP request contains a valid internal API key in its headers.
    /// </summary>
    /// <remarks>This method checks for the presence and validity of the internal API key in the request
    /// headers. If the required header is missing, a warning is logged. The comparison is case-sensitive.</remarks>
    /// <param name="httpRequest">The HTTP request to validate. The request must include the internal API key in the header specified by <see
    /// cref="Constants.HttpAuthorizationSchemeInternalKey"/>.</param>
    /// <returns>true if the request contains a valid internal API key; otherwise, false.</returns>
    private bool CheckApiKey(HttpRequest httpRequest)
    {
        if (iApiKey == null)
            iApiKey = settings.Value.InternalApiKey;

        if (httpRequest.Headers.TryGetValue(Constants.HttpAuthorizationSchemeInternalKey, out Microsoft.Extensions.Primitives.StringValues values))
            return values == iApiKey;
        else
            logger.LogWarning($"No {Constants.HttpAuthorizationSchemeInternalKey} found in grpc header.");

        return false;
    }
}
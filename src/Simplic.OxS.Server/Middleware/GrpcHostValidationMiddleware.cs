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
/// Middleware to validate that gRPC requests only come from allowed hosts
/// </summary>
public class GrpcHostValidationMiddleware(RequestDelegate next
                                        , ILogger<GrpcHostValidationMiddleware> logger
                                        , ICurrentService currentService
                                        , IWebHostEnvironment environment
                                        , IOptions<AuthSettings> settings)
{
    private static string? iApiKey = null;

    /// <summary>
    /// Invoke the middleware to validate gRPC host access
    /// </summary>
    /// <param name="context">Http context</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Get service name from the current service
        var serviceName = currentService.ServiceName;

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

            var host = context.Request.Host.Host;
            var isAllowed = CheckApiKey(context.Request);

            if (!isAllowed)
            {
                logger.LogWarning("gRPC request from unauthorized host: {Host}", host);
                context.Response.StatusCode = 403; // Forbidden
                await context.Response.WriteAsync("Access denied: Host not allowed for gRPC requests");
                return;
            }

            logger.LogDebug("gRPC request from authorized host: {Host}", host);
        }

        await next(context);
    }

    /// <summary>
    /// Check if the request is a gRPC request
    /// </summary>
    private static bool IsGrpcRequest(HttpContext context)
    {
        Console.WriteLine($"Check is gRPC request: {context.Request.ContentType}, {context.Request.Protocol}");

        foreach (var h in context.Request.Headers)
            Console.WriteLine($" Header: {h.Key}: {h.Value}");

        return context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true ||
                  (context.Request.Protocol == "HTTP/2" && context.Request.Headers.ContainsKey("grpc-accept-encoding"));
    }

    /// <summary>
    /// Validate if the host is allowed for gRPC requests
    /// </summary>
    private bool CheckApiKey(HttpRequest httpRequest)
    {
        Console.WriteLine("Validate host access");
        if (iApiKey == null)
            iApiKey = settings.Value.InternalApiKey;

        if (httpRequest.Headers.TryGetValue(Constants.HttpAuthorizationSchemeInternalKey, out Microsoft.Extensions.Primitives.StringValues values))
        {
            Console.WriteLine($" > {values}=={iApiKey}");
            return values == iApiKey;
        }
        else
        {
            Console.WriteLine("No i-api-key found in HostValidation");
        }

        return false;
    }
}
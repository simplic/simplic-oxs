using MassTransit.Caching.Internals;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.Middleware;

/// <summary>
/// Middleware to validate that gRPC requests only come from allowed hosts
/// </summary>
public class GrpcHostValidationMiddleware(RequestDelegate next
                                        , ILogger<GrpcHostValidationMiddleware> logger
                                        , ICurrentService currentService
                                        , IWebHostEnvironment environment)
{
    private static string[] allowedHosts = null;

    /// <summary>
    /// Invoke the middleware to validate gRPC host access
    /// </summary>
    /// <param name="context">Http context</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Get service name from the current service
        var serviceName = currentService.ServiceName;

        // Set list of allowed hosts, defined in bootstrap file
        allowedHosts ??= [$"{currentService.ServiceName}", $"{currentService.ServiceName}-{currentService.ApiVersion}"];

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
            var isAllowed = IsAllowedHost(host, serviceName);

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
        return context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true ||
                  (context.Request.Protocol == "HTTP/2" && context.Request.Headers.ContainsKey("grpc-accept-encoding"));
    }

    /// <summary>
    /// Validate if the host is allowed for gRPC requests
    /// </summary>
    private bool IsAllowedHost(string host, string serviceName)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        // Allow localhost and variations
        if (IsLocalhost(host))
            return true;

        // Allow service-name as domain
        if (string.Equals(host, serviceName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow service-name with common TLDs for service discovery
        var allowedServiceDomains = new[]
     {
            $"{serviceName}",
            $"{serviceName}.local",
            $"{serviceName}.internal",
            $"{serviceName}.cluster.local",
            $"{serviceName}.svc.cluster.local"
        };

        if (allowedServiceDomains.Any(domain => string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Check additional configured hosts
        if (allowedHosts.Any(allowedHost => string.Equals(host, allowedHost, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    /// <summary>
    /// Check if the host is localhost or local IP
    /// </summary>
    private static bool IsLocalhost(string host)
    {
        var localhostVariants = new[]
        {
            "localhost",
            "127.0.0.1",
            "::1",
            "0.0.0.0"
        };

        return localhostVariants.Any(localhost => string.Equals(host, localhost, StringComparison.OrdinalIgnoreCase));
    }
}
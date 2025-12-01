using Microsoft.AspNetCore.Http;
using Simplic.OxS.Server.Telemetry;
using System.Diagnostics;

namespace Simplic.OxS.Server.Middleware;

/// <summary>
/// Middleware for collecting telemetry metrics
/// </summary>
public class TelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TelemetryService _telemetryService;

    public TelemetryMiddleware(RequestDelegate next, TelemetryService telemetryService)
    {
        _next = next;
        _telemetryService = telemetryService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Start a custom activity for tracing
        using var activity = _telemetryService.StartActivity($"HTTP {context.Request.Method} {context.Request.Path}");
        
        // Add tags to the activity
        activity?.SetTag("http.method", context.Request.Method);
        activity?.SetTag("http.url", context.Request.Path);
        activity?.SetTag("user_agent", context.Request.Headers.UserAgent.ToString());

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Record error in activity
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // Add error tag to metrics
            _telemetryService.IncrementRequestCount(
                new KeyValuePair<string, object?>("method", context.Request.Method),
                new KeyValuePair<string, object?>("path", context.Request.Path.ToString()),
                new KeyValuePair<string, object?>("status", context.Response.StatusCode),
                new KeyValuePair<string, object?>("error", "true")
            );
            
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            // Record metrics
            _telemetryService.IncrementRequestCount(
                new KeyValuePair<string, object?>("method", context.Request.Method),
                new KeyValuePair<string, object?>("path", context.Request.Path.ToString()),
                new KeyValuePair<string, object?>("status", context.Response.StatusCode)
            );
            
            _telemetryService.RecordRequestDuration(
                stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("method", context.Request.Method),
                new KeyValuePair<string, object?>("path", context.Request.Path.ToString()),
                new KeyValuePair<string, object?>("status", context.Response.StatusCode)
            );
            
            // Add response status to activity
            activity?.SetTag("http.status_code", context.Response.StatusCode);
        }
    }
}
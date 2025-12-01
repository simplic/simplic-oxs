using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Simplic.OxS.Server.Telemetry;

/// <summary>
/// Service for creating custom telemetry data
/// </summary>
public class TelemetryService : IDisposable
{
    private readonly ActivitySource _activitySource;
    private readonly Meter _meter;
    
    // Metrics
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly UpDownCounter<int> _activeConnections;

    public TelemetryService(string serviceName)
    {
        var name = $"oxs-{serviceName}";
        _activitySource = new ActivitySource(name);
        _meter = new Meter(name);
        
        // Initialize metrics
        _requestCounter = _meter.CreateCounter<long>(
            "oxs_requests_total",
            "requests",
            "Total number of requests processed");
            
        _requestDuration = _meter.CreateHistogram<double>(
            "oxs_request_duration_seconds",
            "seconds", 
            "Request duration in seconds");
            
        _activeConnections = _meter.CreateUpDownCounter<int>(
            "oxs_active_connections",
            "connections",
            "Number of active connections");
    }

    /// <summary>
    /// Start a new activity for tracing
    /// </summary>
    /// <param name="name">Activity name</param>
    /// <returns>Activity instance or null</returns>
    public Activity? StartActivity(string name) => _activitySource.StartActivity(name);

    /// <summary>
    /// Increment request counter
    /// </summary>
    /// <param name="tags">Additional tags for the metric</param>
    public void IncrementRequestCount(params KeyValuePair<string, object?>[] tags)
    {
        _requestCounter.Add(1, tags);
    }

    /// <summary>
    /// Record request duration
    /// </summary>
    /// <param name="durationSeconds">Duration in seconds</param>
    /// <param name="tags">Additional tags for the metric</param>
    public void RecordRequestDuration(double durationSeconds, params KeyValuePair<string, object?>[] tags)
    {
        _requestDuration.Record(durationSeconds, tags);
    }

    /// <summary>
    /// Add active connection
    /// </summary>
    /// <param name="tags">Additional tags for the metric</param>
    public void AddActiveConnection(params KeyValuePair<string, object?>[] tags)
    {
        _activeConnections.Add(1, tags);
    }

    /// <summary>
    /// Remove active connection
    /// </summary>
    /// <param name="tags">Additional tags for the metric</param>
    public void RemoveActiveConnection(params KeyValuePair<string, object?>[] tags)
    {
        _activeConnections.Add(-1, tags);
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
        _meter?.Dispose();
    }
}
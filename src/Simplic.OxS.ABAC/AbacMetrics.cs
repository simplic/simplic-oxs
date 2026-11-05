namespace Simplic.OxS.ABAC;

/// <summary>Meter and instrument names (§16).</summary>
public static class AbacMetrics
{
    /// <summary>Meter name.</summary>
    public const string MeterName = "Simplic.OxS.Abac";
    /// <summary>Counter: decisions, tags decision/action/source.</summary>
    public const string Decisions = "abac_decisions_total";
    /// <summary>Histogram: evaluation latency in ms.</summary>
    public const string EvaluationDuration = "abac_evaluation_duration_ms";
    /// <summary>Counter: policy loads, tag source (redis/local_store/unavailable).</summary>
    public const string PolicyLoads = "abac_policy_loads_total";
    /// <summary>Counter: stale local-store reads.</summary>
    public const string StaleReads = "abac_stale_reads_total";
    /// <summary>Counter: sync runs, tag result (success/failure).</summary>
    public const string SyncRuns = "abac_sync_runs_total";
    /// <summary>Counter: bus messages skipped by consumers.</summary>
    public const string BusSkipped = "abac_bus_skipped";
    /// <summary>Counter: SignalR messages suppressed.</summary>
    public const string SignalRSuppressed = "abac_signalr_suppressed_total";
}
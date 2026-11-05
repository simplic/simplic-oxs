using System.Diagnostics.Metrics;
using Simplic.OxS.ABAC;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// ABAC runtime metrics (§16.1) published on the meter <see cref="AbacMetrics.MeterName"/>.
/// Register the meter with OpenTelemetry via <c>AddMeter(AbacMetrics.MeterName)</c>.
/// Registered as singleton by <c>AddAbacRuntime</c>; components fall back to <see cref="Shared"/>
/// when constructed without DI.
/// </summary>
public sealed class AbacRuntimeMetrics
{
    private static readonly Lazy<AbacRuntimeMetrics> SharedInstance = new(() => new AbacRuntimeMetrics(null));

    private readonly Counter<long> _decisions;
    private readonly Histogram<double> _evaluationDuration;
    private readonly Counter<long> _policyLoads;
    private readonly Counter<long> _staleReads;
    private readonly Counter<long> _syncRuns;
    private readonly Counter<long> _busSkipped;
    private readonly Counter<long> _signalRSuppressed;

    /// <summary>Initializes the instruments, using <paramref name="meterFactory"/> when available.</summary>
    public AbacRuntimeMetrics(IMeterFactory? meterFactory = null)
    {
        Meter = meterFactory?.Create(AbacMetrics.MeterName) ?? new Meter(AbacMetrics.MeterName);

        _decisions = Meter.CreateCounter<long>(AbacMetrics.Decisions, description: "ABAC decisions by mode and result.");
        _evaluationDuration = Meter.CreateHistogram<double>(AbacMetrics.EvaluationDuration, unit: "ms", description: "ABAC evaluation duration.");
        _policyLoads = Meter.CreateCounter<long>(AbacMetrics.PolicyLoads, description: "ABAC policy snapshot loads by source.");
        _staleReads = Meter.CreateCounter<long>(AbacMetrics.StaleReads, description: "ABAC reads of stale (max-age exceeded) or expired (hard-limit) rule sets.");
        _syncRuns = Meter.CreateCounter<long>(AbacMetrics.SyncRuns, description: "ABAC rule-set sync runs by trigger and outcome.");
        _busSkipped = Meter.CreateCounter<long>(AbacMetrics.BusSkipped, description: "Bus messages skipped because of ABAC deny rules.");
        _signalRSuppressed = Meter.CreateCounter<long>(AbacMetrics.SignalRSuppressed, description: "SignalR broadcasts suppressed by ABAC.");
    }

    /// <summary>Process-wide fallback instance.</summary>
    public static AbacRuntimeMetrics Shared => SharedInstance.Value;

    /// <summary>The underlying meter.</summary>
    public Meter Meter { get; }

    /// <summary>Records an ABAC decision.</summary>
    public void RecordDecision(string mode, string decision, string reason)
        => _decisions.Add(1,
            new KeyValuePair<string, object?>("mode", mode),
            new KeyValuePair<string, object?>("decision", decision),
            new KeyValuePair<string, object?>("reason", reason));

    /// <summary>Records the evaluation duration.</summary>
    public void RecordEvaluationDuration(string mode, double milliseconds, bool cacheHit)
        => _evaluationDuration.Record(milliseconds,
            new KeyValuePair<string, object?>("mode", mode),
            new KeyValuePair<string, object?>("cache_hit", cacheHit));

    /// <summary>Records a policy snapshot load (source: redis, local_store, pull, none).</summary>
    public void RecordPolicyLoad(string source)
        => _policyLoads.Add(1, new KeyValuePair<string, object?>("source", source));

    /// <summary>Records a stale read (<paramref name="hardLimitExceeded"/> = set unusable → DENY).</summary>
    public void RecordStaleRead(bool hardLimitExceeded)
        => _staleReads.Add(1, new KeyValuePair<string, object?>("hard_limit", hardLimitExceeded));

    /// <summary>Records a sync run.</summary>
    public void RecordSyncRun(string outcome)
        => _syncRuns.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>Records a skipped bus message.</summary>
    public void RecordBusSkipped(string action)
        => _busSkipped.Add(1, new KeyValuePair<string, object?>("action", action));

    /// <summary>Records a suppressed SignalR broadcast.</summary>
    public void RecordSignalRSuppressed(string action)
        => _signalRSuppressed.Add(1, new KeyValuePair<string, object?>("action", action));
}

using Microsoft.Extensions.Logging;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.ABAC.Runtime;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// Bus egress guard (§7.6): domain events that carry resource data must not leak attributes that ABAC hides
/// from some subjects. Before publishing, a service asks whether deny rules exist for the read action of the
/// payload; if so (or if the rule set is unavailable — fail closed) the message should be skipped or reduced
/// to an id-only notification.
/// <para>
/// <b>Search indexing is exempt</b>: messages consumed by the search indexer must always be published, since the
/// search service applies ABAC at query time via <see cref="IAbacEvaluator.BuildSearchFilterAsync"/>. Do not call
/// this guard for indexing messages.
/// </para>
/// </summary>
public class AbacBusGuard
{
    private readonly IAbacEvaluator _evaluator;
    private readonly ILogger<AbacBusGuard> _logger;
    private readonly AbacRuntimeMetrics _metrics;

    /// <summary>Initializes a new instance of <see cref="AbacBusGuard"/>.</summary>
    public AbacBusGuard(IAbacEvaluator evaluator, ILogger<AbacBusGuard> logger, AbacRuntimeMetrics? metrics = null)
    {
        _evaluator = evaluator;
        _logger = logger;
        _metrics = metrics ?? AbacRuntimeMetrics.Shared;
    }

    /// <summary>
    /// Returns <c>true</c> when a bus message for <paramref name="readAction"/> must be skipped because deny rules
    /// exist for that action (or no rule set is available). Increments <c>abac_bus_skipped</c> and logs.
    /// Not to be used for search indexing messages (exempt).
    /// </summary>
    public async Task<bool> ShouldSkipAsync(Guid organizationId, string readAction, CancellationToken ct = default)
    {
        if (!await _evaluator.HasDenyRulesForActionAsync(organizationId, readAction, ct))
            return false;

        _logger.LogInformation(
            "ABAC: Skipping bus message for action '{Action}' in org '{OrgId}' — deny rules present or rule set unavailable.",
            readAction, organizationId);
        _metrics.RecordBusSkipped(readAction);
        return true;
    }
}

/// <summary>
/// Decides whether a SignalR broadcast carrying a resource may be delivered to the current subject (§7.6).
/// Use from a hub filter or a per-connection broadcaster (the subject is taken from the current request/connection).
/// </summary>
public interface IAbacSignalRFilter
{
    /// <summary>
    /// Returns <c>true</c> when the current subject may receive <paramref name="resource"/> for
    /// <paramref name="action"/>. A suppressed delivery increments <c>abac_signalr_suppressed_total</c>.
    /// </summary>
    Task<bool> CanReceiveAsync(Guid organizationId, string action, IReadOnlyDictionary<string, object?> resource, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IAbacSignalRFilter"/> based on the resource overload of <see cref="IAbacEvaluator.AuthorizeAsync(Guid, string, IReadOnlyDictionary{string, object?}, CancellationToken)"/>.
/// </summary>
public class AbacSignalRBroadcastFilter : IAbacSignalRFilter
{
    private readonly IAbacEvaluator _evaluator;
    private readonly ILogger<AbacSignalRBroadcastFilter> _logger;
    private readonly AbacRuntimeMetrics _metrics;

    /// <summary>Initializes a new instance of <see cref="AbacSignalRBroadcastFilter"/>.</summary>
    public AbacSignalRBroadcastFilter(IAbacEvaluator evaluator, ILogger<AbacSignalRBroadcastFilter> logger, AbacRuntimeMetrics? metrics = null)
    {
        _evaluator = evaluator;
        _logger = logger;
        _metrics = metrics ?? AbacRuntimeMetrics.Shared;
    }

    /// <inheritdoc/>
    public async Task<bool> CanReceiveAsync(Guid organizationId, string action, IReadOnlyDictionary<string, object?> resource, CancellationToken ct = default)
    {
        var result = await _evaluator.AuthorizeAsync(organizationId, action, resource, ct);
        if (result.Decision == AbacDecision.Allow)
            return true;

        _logger.LogDebug("ABAC: SignalR broadcast for action '{Action}' in org '{OrgId}' suppressed.", action, organizationId);
        _metrics.RecordSignalRSuppressed(action);
        return false;
    }
}

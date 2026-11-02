using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.ABAC.Cache;
using Simplic.OxS.ABAC.Sync;
using Simplic.OxS.Data.MongoDB.Abac;

namespace Simplic.OxS.Server.Abac.Sync;

/// <summary>Result of <see cref="IAbacRuleSyncService.SyncAsync"/>.</summary>
public class AbacRuleSyncResult
{
    /// <summary><c>true</c> when a rule set was pulled and applied.</summary>
    public bool Success { get; init; }

    /// <summary>The pulled rule set (when successful).</summary>
    public AbacOrganizationRuleSet? RuleSet { get; init; }

    /// <summary>UTC fetch time.</summary>
    public DateTime FetchedAt { get; init; }

    /// <summary>Error description (when not successful).</summary>
    public string? Error { get; init; }
}

/// <summary>
/// Synchronizes an organization's active rule set from Identity into the service-local rule store
/// (<c>abac.compiled-rules</c>) and the Redis hot cache (§12.6). The sync component is the only writer
/// of the local store.
/// </summary>
public interface IAbacRuleSyncService
{
    /// <summary>
    /// Pulls the active set of <paramref name="organizationId"/> from Identity and writes it to the
    /// local store and Redis. Never throws (except on cancellation); failures are reported in the result.
    /// </summary>
    Task<AbacRuleSyncResult> SyncAsync(Guid organizationId, CancellationToken ct = default);
}

/// <summary>Default <see cref="IAbacRuleSyncService"/>.</summary>
public class AbacRuleSyncService : IAbacRuleSyncService
{
    private readonly IAbacRuleSetClient _client;
    private readonly ILogger<AbacRuleSyncService> _logger;
    private readonly IAbacRuleStore? _store;
    private readonly IDistributedCache? _cache;
    private readonly AbacRuntimeOptions _options;
    private readonly AbacRuntimeMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of <see cref="AbacRuleSyncService"/>.</summary>
    public AbacRuleSyncService(
        IAbacRuleSetClient client,
        ILogger<AbacRuleSyncService> logger,
        IAbacRuleStore? store = null,
        IDistributedCache? cache = null,
        IOptions<AbacRuntimeOptions>? options = null,
        AbacRuntimeMetrics? metrics = null,
        TimeProvider? timeProvider = null)
    {
        _client = client;
        _logger = logger;
        _store = store;
        _cache = cache;
        _options = options?.Value ?? new AbacRuntimeOptions();
        _metrics = metrics ?? AbacRuntimeMetrics.Shared;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<AbacRuleSyncResult> SyncAsync(Guid organizationId, CancellationToken ct = default)
    {
        var fetchedAt = _timeProvider.GetUtcNow().UtcDateTime;
        AbacOrganizationRuleSet? set;
        try
        {
            set = await _client.GetRuleSetAsync(organizationId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ABAC: Rule-set pull for org '{OrgId}' failed.", organizationId);
            _metrics.RecordSyncRun("pull_failed");
            return new AbacRuleSyncResult { FetchedAt = fetchedAt, Error = ex.Message };
        }

        if (set == null)
        {
            _logger.LogWarning("ABAC: Identity returned no rule set for org '{OrgId}'.", organizationId);
            _metrics.RecordSyncRun("not_found");
            return new AbacRuleSyncResult { FetchedAt = fetchedAt, Error = "No rule set returned." };
        }

        if (set.OrganizationId == Guid.Empty)
            set.OrganizationId = organizationId;

        if (set.OrganizationId != organizationId)
        {
            _logger.LogError("ABAC: Pulled rule set belongs to org '{ActualOrg}' instead of '{OrgId}' — rejected.", set.OrganizationId, organizationId);
            _metrics.RecordSyncRun("rejected");
            return new AbacRuleSyncResult { FetchedAt = fetchedAt, Error = "Organization mismatch." };
        }

        if (string.IsNullOrWhiteSpace(set.SetId))
        {
            _logger.LogError("ABAC: Pulled rule set for org '{OrgId}' has no set id — rejected.", organizationId);
            _metrics.RecordSyncRun("rejected");
            return new AbacRuleSyncResult { FetchedAt = fetchedAt, Error = "Missing set id." };
        }

        set.Policies ??= [];
        if (string.IsNullOrWhiteSpace(set.EngineVersion))
            set.EngineVersion = AbacEngineVersion.Current;

        var stored = false;
        if (_store != null)
        {
            try
            {
                await _store.SaveActiveAsync(new AbacStoredRuleSet
                {
                    Id = AbacStoredRuleSet.BuildId(organizationId, set.SetId),
                    OrganizationId = organizationId,
                    SetId = set.SetId,
                    EngineVersion = set.EngineVersion,
                    FetchedAt = fetchedAt,
                    ActivatedAt = set.ActivatedAt,
                    IsActive = true,
                    IsConfirmedEmpty = set.IsConfirmedEmpty,
                    AllowAllWhenEmpty = set.AllowAllWhenEmpty,
                    PayloadJson = AbacRuleSetJson.Serialize(set)
                }, ct);
                stored = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ABAC: Failed to write rule set '{SetId}' of org '{OrgId}' to the local store.", set.SetId, organizationId);
            }
        }

        var cached = false;
        if (_cache != null)
        {
            try
            {
                await AbacRedisRuleSetCache.WriteAsync(_cache, set, _options.Sync.RedisTtl, ct);
                cached = true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ABAC: Failed to refresh Redis hot cache for org '{OrgId}'.", organizationId);
            }
        }

        _logger.LogInformation(
            "ABAC: Synchronized rule set '{SetId}' for org '{OrgId}' ({PolicyCount} policies, store: {Stored}, redis: {Cached}).",
            set.SetId, organizationId, set.Policies.Count, stored, cached);
        _metrics.RecordSyncRun("success");

        return new AbacRuleSyncResult { Success = true, RuleSet = set, FetchedAt = fetchedAt };
    }
}

/// <summary>
/// Per-organization backoff for synchronous on-demand pulls after a failure, so that an unreachable
/// Identity does not add the pull timeout to every request. Registered as singleton.
/// </summary>
public class AbacSyncBackoff
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _blockedUntil = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of <see cref="AbacSyncBackoff"/>.</summary>
    public AbacSyncBackoff(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Returns <c>true</c> when an on-demand pull for the organization is currently suppressed.</summary>
    public bool IsBackedOff(Guid organizationId)
        => _blockedUntil.TryGetValue(organizationId, out var until) && until > _timeProvider.GetUtcNow();

    /// <summary>Records a failed pull.</summary>
    public void RecordFailure(Guid organizationId, TimeSpan backoff)
        => _blockedUntil[organizationId] = _timeProvider.GetUtcNow() + backoff;

    /// <summary>Records a successful pull.</summary>
    public void RecordSuccess(Guid organizationId)
        => _blockedUntil.TryRemove(organizationId, out _);
}

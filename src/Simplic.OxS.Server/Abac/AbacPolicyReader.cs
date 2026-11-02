using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.ABAC.Compiled;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Sync;
using Simplic.OxS.Data.MongoDB.Abac;
using Simplic.OxS.Server.Abac.Sync;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// Loads an organization's active ABAC rule set (§12.6, §12.7).
/// <para>
/// Resolution order:
/// <list type="number">
///   <item><b>Redis hot cache</b> (key scheme of <see cref="Simplic.OxS.ABAC.Cache.AbacCacheKeys"/>: active-set pointer →
///   index → compiled policies, plus the flags key <c>abac:policy:{orgId}:flags:{setId}</c> for confirmed-empty sets).
///   A partially present set is treated as a miss.</item>
///   <item><b>Service-local rule store</b> (<c>abac.compiled-rules</c>): used while younger than <c>Abac:Sync:HardLimit</c>;
///   older than <c>Abac:Sync:MaxAge</c> it is flagged stale (warning + metric). Fresh copies are written back to Redis.</item>
///   <item><b>One synchronous pull</b> from Identity (timeout <c>Abac:Sync:PullTimeout</c>, per-org failure backoff).</item>
///   <item><see cref="AbacPolicySnapshot.Unavailable"/> → the evaluator denies.</item>
/// </list>
/// Rules irrelevant for <c>Abac:Sync:ActionPrefixes</c> are dropped from the returned snapshot.
/// </para>
/// </summary>
public class AbacPolicyReader : IAbacPolicyReader
{
    private readonly ILogger<AbacPolicyReader> _logger;
    private readonly IDistributedCache? _cache;
    private readonly IAbacRuleStore? _store;
    private readonly IAbacRuleSyncService? _syncService;
    private readonly AbacRuntimeOptions _options;
    private readonly AbacRuntimeMetrics _metrics;
    private readonly AbacSyncBackoff? _backoff;
    private readonly TimeProvider _timeProvider;
    private readonly IMemoryCache? _memoryCache;
    private readonly IReadOnlyList<string> _prefixes;

    /// <summary>Initializes a new instance of <see cref="AbacPolicyReader"/>. All sources are optional.</summary>
    public AbacPolicyReader(
        ILogger<AbacPolicyReader> logger,
        IDistributedCache? cache = null,
        IAbacRuleStore? store = null,
        IAbacRuleSyncService? syncService = null,
        IOptions<AbacRuntimeOptions>? options = null,
        AbacRuntimeMetrics? metrics = null,
        AbacSyncBackoff? backoff = null,
        TimeProvider? timeProvider = null,
        IMemoryCache? memoryCache = null)
    {
        _logger = logger;
        _cache = cache;
        _store = store;
        _syncService = syncService;
        _options = options?.Value ?? new AbacRuntimeOptions();
        _metrics = metrics ?? AbacRuntimeMetrics.Shared;
        _backoff = backoff;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _memoryCache = memoryCache;
        _prefixes = _options.Sync.GetNormalizedActionPrefixes();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CompiledPolicy>> GetPoliciesAsync(Guid organizationId, CancellationToken ct = default)
        => (await GetSnapshotAsync(organizationId, ct)).Policies;

    /// <inheritdoc/>
    public async Task<AbacPolicySnapshot> GetSnapshotAsync(Guid organizationId, CancellationToken ct = default)
    {
        var snapshot = await TryRedisAsync(organizationId, ct);
        if (snapshot != null)
        {
            _metrics.RecordPolicyLoad("redis");
            return snapshot;
        }

        snapshot = await TryLocalStoreAsync(organizationId, ct);
        if (snapshot != null)
        {
            _metrics.RecordPolicyLoad("local_store");
            return snapshot;
        }

        snapshot = await TryPullAsync(organizationId, ct);
        if (snapshot != null)
        {
            _metrics.RecordPolicyLoad("pull");
            return snapshot;
        }

        _logger.LogWarning("ABAC: No rule set available for org '{OrgId}' (Redis, local store and pull failed) — denying.", organizationId);
        _metrics.RecordPolicyLoad("none");
        return AbacPolicySnapshot.Unavailable;
    }

    private async Task<AbacPolicySnapshot?> TryRedisAsync(Guid organizationId, CancellationToken ct)
    {
        if (_cache == null)
            return null;

        try
        {
            var setId = await AbacRedisRuleSetCache.ReadActiveSetIdAsync(_cache, organizationId, ct);
            if (setId == null)
                return null;

            var memoKey = $"abac:snapshot:{organizationId:N}:{setId}";
            if (_memoryCache != null && _memoryCache.TryGetValue(memoKey, out AbacPolicySnapshot? memo) && memo != null)
                return memo;

            var set = await AbacRedisRuleSetCache.ReadSetAsync(_cache, organizationId, setId, _logger, ct);
            if (set == null)
                return null;

            var snapshot = AbacSnapshotFactory.FromRuleSet(set, AbacPolicySource.Redis, isStale: false, _prefixes);
            if (_memoryCache != null && _options.L1.Enabled && _options.L1.Ttl > TimeSpan.Zero)
                _memoryCache.Set(memoKey, snapshot, _options.L1.Ttl);

            return snapshot;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ABAC: Redis read failed for org '{OrgId}' — falling back to local store.", organizationId);
            return null;
        }
    }

    private async Task<AbacPolicySnapshot?> TryLocalStoreAsync(Guid organizationId, CancellationToken ct)
    {
        if (_store == null)
            return null;

        AbacStoredRuleSet? stored;
        try
        {
            stored = await _store.GetActiveAsync(organizationId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ABAC: Local rule store read failed for org '{OrgId}'.", organizationId);
            return null;
        }

        if (stored == null)
            return null;

        var age = _timeProvider.GetUtcNow().UtcDateTime - DateTime.SpecifyKind(stored.FetchedAt, DateTimeKind.Utc);
        if (age > _options.Sync.HardLimit)
        {
            _logger.LogError(
                "ABAC: Local rule set '{SetId}' of org '{OrgId}' is older than the hard limit ({Age} > {HardLimit}) — not used.",
                stored.SetId, organizationId, age, _options.Sync.HardLimit);
            _metrics.RecordStaleRead(hardLimitExceeded: true);
            return null;
        }

        AbacOrganizationRuleSet? set;
        try
        {
            set = AbacRuleSetJson.Deserialize<AbacOrganizationRuleSet>(stored.PayloadJson);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "ABAC: Local rule set '{SetId}' of org '{OrgId}' is unreadable.", stored.SetId, organizationId);
            return null;
        }

        if (set == null)
            return null;

        set.OrganizationId = organizationId;
        set.SetId = stored.SetId;
        set.IsConfirmedEmpty = stored.IsConfirmedEmpty;
        set.AllowAllWhenEmpty = stored.AllowAllWhenEmpty;
        set.Policies ??= [];

        var isStale = age > _options.Sync.MaxAge;
        if (isStale)
        {
            _logger.LogWarning(
                "ABAC: Using stale local rule set '{SetId}' of org '{OrgId}' (age {Age} > max-age {MaxAge}).",
                stored.SetId, organizationId, age, _options.Sync.MaxAge);
            _metrics.RecordStaleRead(hardLimitExceeded: false);
        }
        else if (_cache != null)
        {
            await RefillRedisAsync(set, _options.Sync.MaxAge - age, ct);
        }

        return AbacSnapshotFactory.FromRuleSet(set, AbacPolicySource.LocalStore, isStale, _prefixes);
    }

    private async Task<AbacPolicySnapshot?> TryPullAsync(Guid organizationId, CancellationToken ct)
    {
        if (_syncService == null || _backoff?.IsBackedOff(organizationId) == true)
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.Sync.PullTimeout);

        AbacRuleSyncResult result;
        try
        {
            result = await _syncService.SyncAsync(organizationId, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("ABAC: On-demand rule-set pull for org '{OrgId}' timed out after {Timeout}.", organizationId, _options.Sync.PullTimeout);
            result = new AbacRuleSyncResult { Error = "Timeout" };
        }

        if (!result.Success || result.RuleSet == null)
        {
            _backoff?.RecordFailure(organizationId, _options.Sync.FailureBackoff);
            return null;
        }

        _backoff?.RecordSuccess(organizationId);
        return AbacSnapshotFactory.FromRuleSet(result.RuleSet, AbacPolicySource.LocalStore, isStale: false, _prefixes);
    }

    private async Task RefillRedisAsync(AbacOrganizationRuleSet set, TimeSpan remainingFreshness, CancellationToken ct)
    {
        var ttl = remainingFreshness < _options.Sync.RedisTtl ? remainingFreshness : _options.Sync.RedisTtl;
        if (ttl <= TimeSpan.Zero)
            return;

        try
        {
            // Never overwrite a pointer to a different (likely newer) set activated by Identity.
            var current = await AbacRedisRuleSetCache.ReadActiveSetIdAsync(_cache!, set.OrganizationId, ct);
            if (current != null && !string.Equals(current, set.SetId, StringComparison.Ordinal))
                return;

            await AbacRedisRuleSetCache.WriteAsync(_cache!, set, ttl, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ABAC: Failed to refill Redis from local store for org '{OrgId}'.", set.OrganizationId);
        }
    }
}

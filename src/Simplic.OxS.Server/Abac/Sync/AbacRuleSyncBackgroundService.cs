using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.Data.MongoDB.Abac;

namespace Simplic.OxS.Server.Abac.Sync;

/// <summary>
/// Periodic safety-net re-sync (§12.6): every <c>Abac:Sync:Interval</c> (default 5 min) the sets in the
/// local store that are older than <c>Abac:Sync:MaxAge</c> (default 1 h) are pulled again. Only
/// organizations already present in the store are refreshed. Sets older than <c>Abac:Sync:HardLimit</c>
/// (default 24 h) are reported as errors (the reader no longer uses them → DENY).
/// </summary>
public class AbacRuleSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AbacRuleSyncBackgroundService> _logger;
    private readonly AbacRuntimeOptions _options;
    private readonly AbacRuntimeMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of <see cref="AbacRuleSyncBackgroundService"/>.</summary>
    public AbacRuleSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AbacRuleSyncBackgroundService> logger,
        IOptions<AbacRuntimeOptions>? options = null,
        AbacRuntimeMetrics? metrics = null,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options?.Value ?? new AbacRuntimeOptions();
        _metrics = metrics ?? AbacRuntimeMetrics.Shared;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Sync.Enabled)
        {
            _logger.LogInformation("ABAC: Periodic rule-set re-sync disabled (Abac:Sync:Enabled = false).");
            return;
        }

        var interval = _options.Sync.Interval > TimeSpan.Zero ? _options.Sync.Interval : TimeSpan.FromMinutes(5);
        using var timer = new PeriodicTimer(interval, _timeProvider);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ABAC: Periodic rule-set re-sync failed.");
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    /// <summary>
    /// Executes one re-sync pass. Returns the number of organizations that were synchronized successfully.
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetService<IAbacRuleStore>();
        var sync = scope.ServiceProvider.GetService<IAbacRuleSyncService>();
        if (store == null || sync == null)
            return 0;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var due = await store.GetActiveSetsFetchedBeforeAsync(now - _options.Sync.MaxAge, ct);
        var succeeded = 0;

        foreach (var set in due)
        {
            ct.ThrowIfCancellationRequested();

            var result = await sync.SyncAsync(set.OrganizationId, ct);
            if (result.Success)
            {
                succeeded++;
                continue;
            }

            var age = now - set.FetchedAt;
            if (age > _options.Sync.HardLimit)
            {
                _logger.LogError(
                    "ABAC: Rule set '{SetId}' of org '{OrgId}' exceeded the hard limit ({Age}) and could not be refreshed — requests are denied.",
                    set.SetId, set.OrganizationId, age);
                _metrics.RecordStaleRead(hardLimitExceeded: true);
            }
            else
            {
                _logger.LogWarning(
                    "ABAC: Rule set '{SetId}' of org '{OrgId}' is stale ({Age}) and could not be refreshed: {Error}",
                    set.SetId, set.OrganizationId, age, result.Error);
            }
        }

        return succeeded;
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

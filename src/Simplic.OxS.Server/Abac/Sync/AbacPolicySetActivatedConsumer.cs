using MassTransit;
using Microsoft.Extensions.Logging;
using Simplic.OxS.ABAC.Sync;
using Simplic.OxS.MessageBroker;

namespace Simplic.OxS.Server.Abac.Sync;

/// <summary>
/// Consumes <see cref="AbacPolicySetActivated"/> (trigger only) and pulls the new active set via
/// <see cref="IAbacRuleSyncService"/> (§12.6).
/// <para>
/// MassTransit consumer discovery in <c>InitializeMassTransit</c> only scans the <b>entry assembly</b> for
/// <see cref="ConsumerAttribute"/>. To activate this consumer, a service either
/// <list type="bullet">
///   <item>declares an (empty) subclass in its own assembly — the inherited <c>[Consumer]</c> attribute is picked up:
///   <code>public class AbacPolicySetActivatedConsumer : Simplic.OxS.Server.Abac.Sync.AbacPolicySetActivatedConsumer
/// {
///     public AbacPolicySetActivatedConsumer(IAbacRuleSyncService s, ILogger&lt;AbacPolicySetActivatedConsumer&gt; l) : base(s, l) { }
/// }</code></item>
///   <item>or registers it explicitly via the <c>additionalConfiguration</c> callback:
///   <c>cfg.AddConsumer&lt;AbacPolicySetActivatedConsumer&gt;()</c>.</item>
/// </list>
/// </para>
/// </summary>
[Consumer]
public class AbacPolicySetActivatedConsumer : IConsumer<AbacPolicySetActivated>
{
    private readonly IAbacRuleSyncService _syncService;
    private readonly ILogger _logger;

    /// <summary>Initializes a new instance of <see cref="AbacPolicySetActivatedConsumer"/>.</summary>
    public AbacPolicySetActivatedConsumer(IAbacRuleSyncService syncService, ILogger<AbacPolicySetActivatedConsumer> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<AbacPolicySetActivated> context)
    {
        var message = context.Message;
        if (message.OrganizationId == Guid.Empty)
        {
            _logger.LogWarning("ABAC: Received AbacPolicySetActivated without organization id — ignored.");
            return;
        }

        _logger.LogInformation("ABAC: Policy set '{SetId}' activated for org '{OrgId}' — pulling.", message.SetId, message.OrganizationId);

        var result = await _syncService.SyncAsync(message.OrganizationId, context.CancellationToken);
        if (!result.Success)
        {
            // Rethrow so MassTransit retry / error queue semantics apply; the periodic re-sync is the safety net.
            throw new InvalidOperationException($"ABAC: Sync of org '{message.OrganizationId}' failed: {result.Error}");
        }
    }
}

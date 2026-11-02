using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Simplic.OxS.ABAC.Cache;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Sync;
using Simplic.OxS.Data.MongoDB.Abac;
using Simplic.OxS.Server.Abac;
using Simplic.OxS.Server.Abac.Sync;
using Xunit;
using static Simplic.OxS.Server.Test.Abac.AbacTestData;

namespace Simplic.OxS.Server.Test.Abac;

/// <summary>Tests for the sync service, background re-sync, bus guard, SignalR filter and read-filter provider.</summary>
public class AbacRuntimeComponentsTest
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private const string Action = "oxs:contact:read";

    private static AbacOrganizationRuleSet RuleSet(Guid org, string setId = "set-1") => new()
    {
        OrganizationId = org,
        SetId = setId,
        EngineVersion = "1",
        ActivatedAt = Now.UtcDateTime,
        Policies = [Policy(org, "p1", Rule(AbacEffect.Allow, [Action]))]
    };

    // ── Sync service ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_WritesStoreAndRedis()
    {
        var client = new Mock<IAbacRuleSetClient>();
        client.Setup(c => c.GetRuleSetAsync(OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(RuleSet(OrgId));
        var store = new Mock<IAbacRuleStore>();
        AbacStoredRuleSet? saved = null;
        store.Setup(s => s.SaveActiveAsync(It.IsAny<AbacStoredRuleSet>(), It.IsAny<CancellationToken>()))
             .Callback<AbacStoredRuleSet, CancellationToken>((s, _) => saved = s)
             .Returns(Task.CompletedTask);
        var cache = NewDistributedCache();

        var sync = new AbacRuleSyncService(client.Object, Mock.Of<ILogger<AbacRuleSyncService>>(), store.Object, cache,
            Options(), new AbacRuntimeMetrics(), new TestTimeProvider(Now));

        var result = await sync.SyncAsync(OrgId);

        result.Success.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.Id.Should().Be(AbacStoredRuleSet.BuildId(OrgId, "set-1"));
        saved.IsActive.Should().BeTrue();
        saved.FetchedAt.Should().Be(Now.UtcDateTime);
        saved.PayloadJson.Should().Contain("\"policyId\":\"p1\"");

        (await cache.GetStringAsync(AbacCacheKeys.ActiveSet(OrgId))).Should().Be("set-1");
        (await cache.GetStringAsync(AbacCacheKeys.Flags(OrgId, "set-1"))).Should().Be("{\"isConfirmedEmpty\":false,\"allowAllWhenEmpty\":false}");
        var roundTrip = await AbacRedisRuleSetCache.ReadSetAsync(cache, OrgId, "set-1", Mock.Of<ILogger>(), default);
        roundTrip!.Policies.Should().ContainSingle(p => p.PolicyId == "p1");
    }

    [Fact]
    public async Task Sync_OrganizationMismatch_IsRejected()
    {
        var client = new Mock<IAbacRuleSetClient>();
        client.Setup(c => c.GetRuleSetAsync(OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(RuleSet(Guid.NewGuid()));
        var store = new Mock<IAbacRuleStore>();

        var result = await new AbacRuleSyncService(client.Object, Mock.Of<ILogger<AbacRuleSyncService>>(), store.Object).SyncAsync(OrgId);

        result.Success.Should().BeFalse();
        store.Verify(s => s.SaveActiveAsync(It.IsAny<AbacStoredRuleSet>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sync_ClientThrows_ReturnsFailure()
    {
        var client = new Mock<IAbacRuleSetClient>();
        client.Setup(c => c.GetRuleSetAsync(OrgId, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));

        var result = await new AbacRuleSyncService(client.Object, Mock.Of<ILogger<AbacRuleSyncService>>()).SyncAsync(OrgId);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("down");
    }

    [Fact]
    public async Task Sync_StoreFailure_StillWritesRedis()
    {
        var client = new Mock<IAbacRuleSetClient>();
        client.Setup(c => c.GetRuleSetAsync(OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(RuleSet(OrgId));
        var store = new Mock<IAbacRuleStore>();
        store.Setup(s => s.SaveActiveAsync(It.IsAny<AbacStoredRuleSet>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("mongo down"));
        var cache = NewDistributedCache();

        var result = await new AbacRuleSyncService(client.Object, Mock.Of<ILogger<AbacRuleSyncService>>(), store.Object, cache).SyncAsync(OrgId);

        result.Success.Should().BeTrue();
        (await cache.GetStringAsync(AbacCacheKeys.ActiveSet(OrgId))).Should().Be("set-1");
    }

    [Fact]
    public async Task Consumer_TriggersSync()
    {
        var sync = new Mock<IAbacRuleSyncService>();
        sync.Setup(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(new AbacRuleSyncResult { Success = true });
        var context = new Mock<MassTransit.ConsumeContext<AbacPolicySetActivated>>();
        context.SetupGet(c => c.Message).Returns(new AbacPolicySetActivated { OrganizationId = OrgId, SetId = "set-2" });
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await new AbacPolicySetActivatedConsumer(sync.Object, Mock.Of<ILogger<AbacPolicySetActivatedConsumer>>()).Consume(context.Object);

        sync.Verify(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Background re-sync ────────────────────────────────────────────────────

    [Fact]
    public async Task BackgroundService_ResyncsSetsOlderThanMaxAge()
    {
        var store = new Mock<IAbacRuleStore>();
        store.Setup(s => s.GetActiveSetsFetchedBeforeAsync(Now.UtcDateTime.AddHours(-1), It.IsAny<CancellationToken>()))
             .ReturnsAsync([new AbacStoredRuleSet { OrganizationId = OrgId, SetId = "set-1", FetchedAt = Now.UtcDateTime.AddHours(-2) }]);
        var sync = new Mock<IAbacRuleSyncService>();
        sync.Setup(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(new AbacRuleSyncResult { Success = true });

        var provider = new ServiceCollection()
            .AddScoped(_ => store.Object)
            .AddScoped(_ => sync.Object)
            .BuildServiceProvider();

        var service = new AbacRuleSyncBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<AbacRuleSyncBackgroundService>>(), Options(), new AbacRuntimeMetrics(), new TestTimeProvider(Now));

        (await service.RunOnceAsync(default)).Should().Be(1);
        sync.Verify(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Bus guard / SignalR / read-filter provider ────────────────────────────

    [Fact]
    public async Task BusGuard_SkipsWhenDenyRulesExist()
    {
        var evaluator = new Mock<IAbacEvaluator>();
        evaluator.Setup(e => e.HasDenyRulesForActionAsync(OrgId, Action, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        evaluator.Setup(e => e.HasDenyRulesForActionAsync(OrgId, "oxs:invoice:read", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var guard = new AbacBusGuard(evaluator.Object, Mock.Of<ILogger<AbacBusGuard>>(), new AbacRuntimeMetrics());

        (await guard.ShouldSkipAsync(OrgId, Action)).Should().BeTrue();
        (await guard.ShouldSkipAsync(OrgId, "oxs:invoice:read")).Should().BeFalse();
    }

    [Fact]
    public async Task SignalRFilter_UsesResourceAuthorization()
    {
        var allowed = new Dictionary<string, object?> { ["department"] = "hr" };
        var denied = new Dictionary<string, object?> { ["department"] = "ops" };
        var evaluator = new Mock<IAbacEvaluator>();
        evaluator.Setup(e => e.AuthorizeAsync(OrgId, Action, allowed, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new AbacDecisionResult { Decision = AbacDecision.Allow });
        evaluator.Setup(e => e.AuthorizeAsync(OrgId, Action, denied, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new AbacDecisionResult { Decision = AbacDecision.Deny });
        var filter = new AbacSignalRBroadcastFilter(evaluator.Object, Mock.Of<ILogger<AbacSignalRBroadcastFilter>>(), new AbacRuntimeMetrics());

        (await filter.CanReceiveAsync(OrgId, Action, allowed)).Should().BeTrue();
        (await filter.CanReceiveAsync(OrgId, Action, denied)).Should().BeFalse();
    }

    [Fact]
    public async Task ReadFilterProvider_NoOrganization_Denies_OtherwiseDelegates()
    {
        var evaluator = new Mock<IAbacEvaluator>();
        evaluator.Setup(e => e.BuildListFilterAsync(OrgId, Action, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new AbacDecisionResult { Decision = AbacDecision.Allow, MongoDbFilterJson = "{\"a\":1}" });
        var context = new Mock<IRequestContext>();

        var provider = new AbacReadFilterProvider(evaluator.Object, context.Object);
        (await provider.GetReadFilterAsync(Action)).Decision.Should().Be(AbacDecision.Deny);

        context.SetupGet(c => c.OrganizationId).Returns(OrgId);
        (await provider.GetReadFilterAsync(Action)).MongoDbFilterJson.Should().Be("{\"a\":1}");
    }
}

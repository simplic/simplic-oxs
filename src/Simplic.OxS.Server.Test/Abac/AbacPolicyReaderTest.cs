using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
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

/// <summary>Tests for <see cref="AbacPolicyReader"/>: fallback order, staleness, prefix filtering, JSON casing.</summary>
public class AbacPolicyReaderTest
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly IDistributedCache _cache = NewDistributedCache();
    private readonly Mock<IAbacRuleStore> _store = new();
    private readonly Mock<IAbacRuleSyncService> _sync = new();
    private readonly TestTimeProvider _time = new(Now);

    private AbacPolicyReader CreateReader(Action<AbacRuntimeOptions>? configure = null, bool withCache = true, bool withStore = true, bool withSync = true, AbacSyncBackoff? backoff = null)
        => new(
            Mock.Of<ILogger<AbacPolicyReader>>(),
            withCache ? _cache : null,
            withStore ? _store.Object : null,
            withSync ? _sync.Object : null,
            Options(configure),
            new AbacRuntimeMetrics(),
            backoff,
            _time);

    private static AbacOrganizationRuleSet RuleSet(string setId = "set-1", bool confirmedEmpty = false, bool allowAll = false, string[][]? rulesActions = null)
        => new()
        {
            OrganizationId = OrgId,
            SetId = setId,
            EngineVersion = "1",
            IsConfirmedEmpty = confirmedEmpty,
            AllowAllWhenEmpty = allowAll,
            Policies = (rulesActions ?? []).Select((a, i) => Policy(OrgId, $"p{i}", Rule(AbacEffect.Allow, a))).ToList()
        };

    private static AbacStoredRuleSet Stored(AbacOrganizationRuleSet set, DateTime fetchedAt) => new()
    {
        Id = AbacStoredRuleSet.BuildId(OrgId, set.SetId),
        OrganizationId = OrgId,
        SetId = set.SetId,
        EngineVersion = "1",
        FetchedAt = fetchedAt,
        IsActive = true,
        IsConfirmedEmpty = set.IsConfirmedEmpty,
        AllowAllWhenEmpty = set.AllowAllWhenEmpty,
        PayloadJson = AbacRuleSetJson.Serialize(set)
    };

    [Fact]
    public async Task RedisHit_ReturnsLoadedFromRedis_WithoutTouchingStoreOrPull()
    {
        await AbacRedisRuleSetCache.WriteAsync(_cache, RuleSet(rulesActions: [["oxs:contact:read"]]), TimeSpan.FromMinutes(5), default);

        var snapshot = await CreateReader().GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.Loaded);
        snapshot.Source.Should().Be(AbacPolicySource.Redis);
        snapshot.SetId.Should().Be("set-1");
        snapshot.Policies.Should().HaveCount(1);
        _store.Verify(s => s.GetActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _sync.Verify(s => s.SyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Redis_CamelCaseJsonWrittenByIdentity_IsDeserialized()
    {
        await _cache.SetStringAsync(AbacCacheKeys.ActiveSet(OrgId), "set-1");
        await _cache.SetStringAsync(AbacCacheKeys.PolicyIndex(OrgId, "1", "set-1"), "[\"p1\"]");
        await _cache.SetStringAsync(AbacCacheKeys.CompiledPolicy(OrgId, "p1", "1", "set-1"),
            "{\"organizationId\":\"" + OrgId + "\",\"policyId\":\"p1\",\"compilerVersion\":\"1\",\"setId\":\"set-1\"," +
            "\"compiledRules\":[{\"ruleId\":\"r1\",\"effect\":\"Deny\",\"actionPatterns\":[\"oxs:contact:read\"],\"mongoDbFilterJson\":\"{\\\"a\\\":1}\"," +
            "\"metadata\":{\"hasResourceConditions\":true}}]}");

        var snapshot = await CreateReader().GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.Loaded);
        var rule = snapshot.Policies.Single().CompiledRules.Single();
        rule.RuleId.Should().Be("r1");
        rule.Effect.Should().Be(AbacEffect.Deny);
        rule.ActionPatterns.Should().Equal("oxs:contact:read");
        rule.MongoDbFilterJson.Should().Be("{\"a\":1}");
        rule.Metadata.HasResourceConditions.Should().BeTrue();
        snapshot.Policies.Single().CompilerVersion.Should().Be("1");
    }

    [Fact]
    public async Task Redis_FlagsConfirmedEmpty_ReturnsConfirmedEmpty()
    {
        await _cache.SetStringAsync(AbacCacheKeys.ActiveSet(OrgId), "set-e");
        await _cache.SetStringAsync(AbacCacheKeys.Flags(OrgId, "set-e"), "{\"isConfirmedEmpty\":true,\"allowAllWhenEmpty\":true}");

        var snapshot = await CreateReader().GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.ConfirmedEmpty);
        snapshot.AllowAllWhenEmpty.Should().BeTrue();
        snapshot.Source.Should().Be(AbacPolicySource.Redis);
        AbacCacheKeys.Flags(OrgId, "set-e").Should().Be($"abac:policy:{OrgId}:flags:set-e");
    }

    [Fact]
    public async Task Redis_PartialSet_IsTreatedAsMiss_AndFallsBackToStore()
    {
        await _cache.SetStringAsync(AbacCacheKeys.ActiveSet(OrgId), "set-1");
        await _cache.SetStringAsync(AbacCacheKeys.PolicyIndex(OrgId, "1", "set-1"), "[\"p1\",\"p-missing\"]");
        _store.Setup(s => s.GetActiveAsync(OrgId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Stored(RuleSet(rulesActions: [["oxs:contact:read"]]), Now.UtcDateTime.AddMinutes(-5)));

        var snapshot = await CreateReader().GetSnapshotAsync(OrgId);

        snapshot.Source.Should().Be(AbacPolicySource.LocalStore);
        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.Loaded);
    }

    [Fact]
    public async Task RedisMiss_FreshStore_ReturnsLocalStore_AndRefillsRedis()
    {
        _store.Setup(s => s.GetActiveAsync(OrgId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Stored(RuleSet(rulesActions: [["oxs:contact:read"]]), Now.UtcDateTime.AddMinutes(-10)));

        var reader = CreateReader();
        var first = await reader.GetSnapshotAsync(OrgId);

        first.Source.Should().Be(AbacPolicySource.LocalStore);
        first.IsStale.Should().BeFalse();
        (await _cache.GetStringAsync(AbacCacheKeys.ActiveSet(OrgId))).Should().Be("set-1");

        var second = await reader.GetSnapshotAsync(OrgId);
        second.Source.Should().Be(AbacPolicySource.Redis);
    }

    [Fact]
    public async Task Store_OlderThanMaxAge_IsStale_AndNotWrittenToRedis()
    {
        _store.Setup(s => s.GetActiveAsync(OrgId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Stored(RuleSet(rulesActions: [["oxs:contact:read"]]), Now.UtcDateTime.AddHours(-2)));

        var snapshot = await CreateReader().GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.Loaded);
        snapshot.Source.Should().Be(AbacPolicySource.LocalStore);
        snapshot.IsStale.Should().BeTrue();
        (await _cache.GetStringAsync(AbacCacheKeys.ActiveSet(OrgId))).Should().BeNull();
        _sync.Verify(s => s.SyncAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Store_OlderThanHardLimit_IsNotUsed_PullFails_Unavailable()
    {
        _store.Setup(s => s.GetActiveAsync(OrgId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Stored(RuleSet(rulesActions: [["oxs:contact:read"]]), Now.UtcDateTime.AddHours(-25)));
        _sync.Setup(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AbacRuleSyncResult { Success = false, Error = "down" });

        var snapshot = await CreateReader().GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.Unavailable);
        _sync.Verify(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Store_CustomMaxAgeAndHardLimit_AreRespected()
    {
        _store.Setup(s => s.GetActiveAsync(OrgId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Stored(RuleSet(rulesActions: [["oxs:contact:read"]]), Now.UtcDateTime.AddMinutes(-20)));

        var stale = await CreateReader(o => o.Sync.MaxAge = TimeSpan.FromMinutes(15), withSync: false).GetSnapshotAsync(OrgId);
        stale.IsStale.Should().BeTrue();
        stale.Status.Should().Be(AbacPolicySnapshotStatus.Loaded);

        var expired = await CreateReader(o => { o.Sync.MaxAge = TimeSpan.FromMinutes(5); o.Sync.HardLimit = TimeSpan.FromMinutes(10); }, withSync: false)
            .GetSnapshotAsync(OrgId);
        expired.Status.Should().Be(AbacPolicySnapshotStatus.Unavailable);
    }

    [Fact]
    public async Task Miss_PullSucceeds_ReturnsPulledSet()
    {
        _sync.Setup(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AbacRuleSyncResult { Success = true, RuleSet = RuleSet("set-9", rulesActions: [["oxs:contact:read"]]) });

        var snapshot = await CreateReader().GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.Loaded);
        snapshot.SetId.Should().Be("set-9");
        snapshot.Source.Should().Be(AbacPolicySource.LocalStore);
    }

    [Fact]
    public async Task Miss_PullReturnsConfirmedEmpty_ReturnsConfirmedEmpty()
    {
        _sync.Setup(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AbacRuleSyncResult { Success = true, RuleSet = RuleSet("set-e", confirmedEmpty: true, allowAll: true) });

        var snapshot = await CreateReader().GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.ConfirmedEmpty);
        snapshot.AllowAllWhenEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task NoSourcesAtAll_Unavailable()
    {
        var snapshot = await CreateReader(withCache: false, withStore: false, withSync: false).GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.Unavailable);
        (await CreateReader(withCache: false, withStore: false, withSync: false).GetPoliciesAsync(OrgId)).Should().BeEmpty();
    }

    [Fact]
    public async Task PullTimeout_ReturnsUnavailable()
    {
        _sync.Setup(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()))
             .Returns<Guid, CancellationToken>(async (_, ct) =>
             {
                 await Task.Delay(Timeout.Infinite, ct);
                 return new AbacRuleSyncResult();
             });

        var snapshot = await CreateReader(o => o.Sync.PullTimeout = TimeSpan.FromMilliseconds(50)).GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.Unavailable);
    }

    [Fact]
    public async Task PullFailure_BacksOff_SecondRequestDoesNotPull()
    {
        _sync.Setup(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new AbacRuleSyncResult { Success = false });
        var backoff = new AbacSyncBackoff(_time);

        await CreateReader(backoff: backoff).GetSnapshotAsync(OrgId);
        await CreateReader(backoff: backoff).GetSnapshotAsync(OrgId);
        _sync.Verify(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()), Times.Once);

        _time.Now = Now.AddMinutes(1);
        await CreateReader(backoff: backoff).GetSnapshotAsync(OrgId);
        _sync.Verify(s => s.SyncAsync(OrgId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ActionPrefixes_DropIrrelevantRules_KeepWildcards()
    {
        var set = RuleSet(rulesActions:
        [
            ["oxs:contact:read"],
            ["oxs:invoice:read"],
            ["*"],
            ["oxs:*"],
            ["oxs:contact:*"],
            ["oxs:invoice:*", "oxs:contact:update"]
        ]);
        await AbacRedisRuleSetCache.WriteAsync(_cache, set, TimeSpan.FromMinutes(5), default);

        var snapshot = await CreateReader(o => o.Sync.ActionPrefixes = ["oxs:contact:"]).GetSnapshotAsync(OrgId);

        snapshot.Policies.Select(p => p.PolicyId).Should().BeEquivalentTo(["p0", "p2", "p3", "p4", "p5"]);
        set.Policies.Should().HaveCount(6, "the stored/cached set itself must stay complete");
    }

    [Fact]
    public async Task ActionPrefixes_AllRulesFiltered_StaysLoaded_NotConfirmedEmpty()
    {
        await AbacRedisRuleSetCache.WriteAsync(_cache, RuleSet(rulesActions: [["oxs:invoice:read"]]), TimeSpan.FromMinutes(5), default);

        var snapshot = await CreateReader(o => o.Sync.ActionPrefixes = ["oxs:contact:"]).GetSnapshotAsync(OrgId);

        snapshot.Status.Should().Be(AbacPolicySnapshotStatus.Loaded);
        snapshot.Policies.Should().BeEmpty();
    }

    [Theory]
    [InlineData("oxs:contact:read", true)]
    [InlineData("oxs:contact:*", true)]
    [InlineData("oxs:con*", true)]
    [InlineData("oxs:*", true)]
    [InlineData("*", true)]
    [InlineData("oxs:invoice:read", false)]
    [InlineData("oxs:invoice:*", false)]
    public void ActionPrefixFilter_IsRelevant(string pattern, bool expected)
        => AbacActionPrefixFilter.IsRelevant([pattern], ["oxs:contact:"]).Should().Be(expected);

    [Fact]
    public void ActionPrefixes_CommaSeparated_AreNormalized()
    {
        var options = new AbacSyncOptions { ActionPrefixes = ["oxs:contact:, oxs:person:", "oxs:contact:"] };
        options.GetNormalizedActionPrefixes().Should().Equal("oxs:contact:", "oxs:person:");
    }
}

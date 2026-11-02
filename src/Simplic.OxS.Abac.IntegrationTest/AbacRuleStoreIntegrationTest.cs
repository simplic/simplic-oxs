using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Simplic.OxS.ABAC.Cache;
using Simplic.OxS.ABAC.Compiled;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Sync;
using Simplic.OxS.Data.MongoDB.Abac;
using Simplic.OxS.Server.Abac;
using Simplic.OxS.Server.Abac.Sync;
using Xunit;

namespace Simplic.OxS.Abac.IntegrationTest;

/// <summary>
/// Integration tests for the service-local rule store (<c>abac.compiled-rules</c>) and the reader fallback
/// chain against real MongoDB and Redis containers. Skipped automatically without Docker.
/// </summary>
[Collection(AbacContainersCollection.Name)]
public class AbacRuleStoreIntegrationTest
{
    private readonly AbacContainersFixture _fixture;

    public AbacRuleStoreIntegrationTest(AbacContainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static AbacOrganizationRuleSet RuleSet(Guid org, string setId, bool confirmedEmpty = false) => new()
    {
        OrganizationId = org,
        SetId = setId,
        EngineVersion = AbacEngineVersion.Current,
        ActivatedAt = DateTime.UtcNow,
        IsConfirmedEmpty = confirmedEmpty,
        Policies = confirmedEmpty
            ? []
            :
            [
                new CompiledPolicy
                {
                    OrganizationId = org,
                    PolicyId = "p1",
                    CompilerVersion = AbacEngineVersion.Current,
                    SetId = setId,
                    CompiledRules =
                    [
                        new CompiledRule
                        {
                            RuleId = "r1",
                            Effect = AbacEffect.Allow,
                            ActionPatterns = ["oxs:contact:read"],
                            MongoDbFilterJson = "{\"department\":\"hr\"}",
                            ResourceConditions = new RuleConditions
                            {
                                RawJson = new Dictionary<string, object> { ["department"] = new Dictionary<string, object> { ["$eq"] = "hr" } }
                            },
                            Metadata = new RuleMetadata { HasResourceConditions = true }
                        }
                    ]
                }
            ]
    };

    private static AbacStoredRuleSet Stored(AbacOrganizationRuleSet set, DateTime fetchedAt) => new()
    {
        Id = AbacStoredRuleSet.BuildId(set.OrganizationId, set.SetId),
        OrganizationId = set.OrganizationId,
        SetId = set.SetId,
        EngineVersion = set.EngineVersion,
        FetchedAt = fetchedAt,
        ActivatedAt = set.ActivatedAt,
        IsActive = true,
        IsConfirmedEmpty = set.IsConfirmedEmpty,
        AllowAllWhenEmpty = set.AllowAllWhenEmpty,
        PayloadJson = AbacRuleSetJson.Serialize(set)
    };

    [SkippableFact]
    public async Task Store_KeepsOnlyActiveAndPreviousSet()
    {
        _fixture.SkipIfUnavailable();

        var org = Guid.NewGuid();
        var context = _fixture.CreateMongoContext($"abac-it-{Guid.NewGuid():N}");
        var store = new MongoAbacRuleStore(context);
        var start = DateTime.UtcNow.AddMinutes(-10);

        await store.SaveActiveAsync(Stored(RuleSet(org, "s1"), start));
        await store.SaveActiveAsync(Stored(RuleSet(org, "s2"), start.AddMinutes(1)));
        await store.SaveActiveAsync(Stored(RuleSet(org, "s3"), start.AddMinutes(2)));

        var active = await store.GetActiveAsync(org);
        active!.SetId.Should().Be("s3");

        var all = await context.GetCollection<AbacStoredRuleSet>(MongoAbacRuleStore.CollectionName)
            .Find(x => x.OrganizationId == org).ToListAsync();
        all.Select(x => x.SetId).Should().BeEquivalentTo(["s3", "s2"]);
        all.Single(x => x.SetId == "s2").IsActive.Should().BeFalse();
    }

    [SkippableFact]
    public async Task Store_GetActiveSetsFetchedBefore_ReturnsOnlyOldActiveSets()
    {
        _fixture.SkipIfUnavailable();

        var context = _fixture.CreateMongoContext($"abac-it-{Guid.NewGuid():N}");
        var store = new MongoAbacRuleStore(context);
        var oldOrg = Guid.NewGuid();
        var freshOrg = Guid.NewGuid();

        await store.SaveActiveAsync(Stored(RuleSet(oldOrg, "old"), DateTime.UtcNow.AddHours(-2)));
        await store.SaveActiveAsync(Stored(RuleSet(freshOrg, "fresh"), DateTime.UtcNow));

        var due = await store.GetActiveSetsFetchedBeforeAsync(DateTime.UtcNow.AddHours(-1));

        due.Select(x => x.OrganizationId).Should().Equal(oldOrg);
    }

    [SkippableFact]
    public async Task Reader_FallsBackToStore_RefillsRedis_ThenServesFromRedis()
    {
        _fixture.SkipIfUnavailable();

        var org = Guid.NewGuid();
        var store = new MongoAbacRuleStore(_fixture.CreateMongoContext($"abac-it-{Guid.NewGuid():N}"));
        using var redis = _fixture.CreateRedisCache();
        await store.SaveActiveAsync(Stored(RuleSet(org, "s1"), DateTime.UtcNow.AddMinutes(-1)));

        var reader = new AbacPolicyReader(NullLogger<AbacPolicyReader>.Instance, redis, store,
            syncService: null, Options.Create(new AbacRuntimeOptions()), new AbacRuntimeMetrics());

        var first = await reader.GetSnapshotAsync(org);
        first.Status.Should().Be(AbacPolicySnapshotStatus.Loaded);
        first.Source.Should().Be(AbacPolicySource.LocalStore);
        first.Policies.Single().CompiledRules.Single().MongoDbFilterJson.Should().Be("{\"department\":\"hr\"}");

        (await redis.GetStringAsync(AbacCacheKeys.ActiveSet(org))).Should().Be("s1");

        var second = await reader.GetSnapshotAsync(org);
        second.Source.Should().Be(AbacPolicySource.Redis);
        second.Policies.Single().CompiledRules.Single().RuleId.Should().Be("r1");
    }

    [SkippableFact]
    public async Task SyncService_WritesStoreAndRedis_ConfirmedEmptyIsReadBack()
    {
        _fixture.SkipIfUnavailable();

        var org = Guid.NewGuid();
        var store = new MongoAbacRuleStore(_fixture.CreateMongoContext($"abac-it-{Guid.NewGuid():N}"));
        using var redis = _fixture.CreateRedisCache();
        var client = new Mock<IAbacRuleSetClient>();
        client.Setup(c => c.GetRuleSetAsync(org, It.IsAny<CancellationToken>())).ReturnsAsync(RuleSet(org, "empty", confirmedEmpty: true));

        var result = await new AbacRuleSyncService(client.Object, NullLogger<AbacRuleSyncService>.Instance, store, redis).SyncAsync(org);
        result.Success.Should().BeTrue();

        (await store.GetActiveAsync(org))!.IsConfirmedEmpty.Should().BeTrue();

        var redisSnapshot = await new AbacPolicyReader(NullLogger<AbacPolicyReader>.Instance, redis).GetSnapshotAsync(org);
        redisSnapshot.Status.Should().Be(AbacPolicySnapshotStatus.ConfirmedEmpty);
        redisSnapshot.Source.Should().Be(AbacPolicySource.Redis);

        var storeSnapshot = await new AbacPolicyReader(NullLogger<AbacPolicyReader>.Instance, store: store).GetSnapshotAsync(org);
        storeSnapshot.Status.Should().Be(AbacPolicySnapshotStatus.ConfirmedEmpty);
        storeSnapshot.Source.Should().Be(AbacPolicySource.LocalStore);
    }
}

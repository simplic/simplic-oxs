using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Simplic.OxS.ABAC.Compiled;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Subject;
using Simplic.OxS.Server.Abac;
using Xunit;
using static Simplic.OxS.Server.Test.Abac.AbacTestData;

namespace Simplic.OxS.Server.Test.Abac;

/// <summary>
/// Tests for the snapshot-based <see cref="AbacEvaluator"/>: rollout fallback, authorize/filter split,
/// <c>$nor</c> deny filters, resource overload, subject variables, L1 cache and deny-rule lookup.
/// </summary>
public class AbacEvaluatorRuntimeTest
{
    private const string Action = "oxs:contact:read";
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid TeamA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TeamB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly Mock<IAbacPolicyReader> _reader = new();
    private readonly Mock<IAbacSubjectFactory> _subjectFactory = new();

    public AbacEvaluatorRuntimeTest()
    {
        _subjectFactory.Setup(f => f.BuildSubject()).Returns(Subject(OrgId, ["viewer"], (TeamA, "Sales"), (TeamB, "Support")));
    }

    private AbacEvaluator CreateEvaluator(bool withL1 = false)
        => new(_reader.Object, _subjectFactory.Object, Mock.Of<ILogger<AbacEvaluator>>(),
            withL1 ? NewMemoryCache() : null, Options(), new AbacRuntimeMetrics());

    private void Snapshot(AbacPolicySnapshot? snapshot)
        => _reader.Setup(r => r.GetSnapshotAsync(OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot!);

    private void Loaded(params CompiledRule[] rules)
        => Snapshot(new AbacPolicySnapshot
        {
            Status = AbacPolicySnapshotStatus.Loaded,
            Source = AbacPolicySource.Redis,
            SetId = "set-1",
            Policies = [Policy(OrgId, "p1", rules)]
        });

    // ── Gates / rollout fallback ──────────────────────────────────────────────

    [Fact]
    public async Task Unavailable_DeniesEverything()
    {
        Snapshot(AbacPolicySnapshot.Unavailable);
        var evaluator = CreateEvaluator();

        (await evaluator.AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);
        (await evaluator.BuildListFilterAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);
        (await evaluator.BuildSearchFilterAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);
        (await evaluator.AuthorizeAsync(OrgId, Action, new Dictionary<string, object?>())).Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task NullSnapshot_Denies()
    {
        Snapshot(null);
        (await CreateEvaluator().AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task ConfirmedEmpty_WithAllowAllWhenEmpty_AllowsWithoutFilter()
    {
        Snapshot(new AbacPolicySnapshot { Status = AbacPolicySnapshotStatus.ConfirmedEmpty, SetId = "e", AllowAllWhenEmpty = true });
        var evaluator = CreateEvaluator();

        var list = await evaluator.BuildListFilterAsync(OrgId, Action);
        list.Decision.Should().Be(AbacDecision.Allow);
        list.MongoDbFilterJson.Should().BeNull();
        (await evaluator.AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Allow);
        (await evaluator.BuildSearchFilterAsync(OrgId, Action)).SearchFilterJson.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmedEmpty_WithoutAllowAllWhenEmpty_Denies()
    {
        Snapshot(new AbacPolicySnapshot { Status = AbacPolicySnapshotStatus.ConfirmedEmpty, SetId = "e", AllowAllWhenEmpty = false });
        (await CreateEvaluator().AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task VersionMismatch_DeniesWithFlag()
    {
        var policy = Policy(OrgId, "p1", Rule(AbacEffect.Allow, [Action]));
        policy.CompilerVersion = "99";
        Snapshot(new AbacPolicySnapshot { Status = AbacPolicySnapshotStatus.Loaded, SetId = "s", Policies = [policy] });

        var result = await CreateEvaluator().BuildListFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
        result.IsVersionMismatch.Should().BeTrue();
    }

    // ── Authorize vs. filter split ────────────────────────────────────────────

    [Fact]
    public async Task Authorize_ConditionalDeny_DoesNotBlockActionLevel()
    {
        Loaded(
            Rule(AbacEffect.Deny, [Action], mongoFilter: "{\"confidential\":true}"),
            Rule(AbacEffect.Allow, [Action]));

        var result = await CreateEvaluator().AuthorizeAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.MongoDbFilterJson.Should().BeNull();
    }

    [Fact]
    public async Task Authorize_UnconditionalDeny_Blocks()
    {
        Loaded(Rule(AbacEffect.Deny, [Action]), Rule(AbacEffect.Allow, [Action]));

        (await CreateEvaluator().AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task Authorize_AllowWithResourceConditions_AllowsOnActionLevel()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], mongoFilter: "{\"department\":\"hr\"}"));

        var result = await CreateEvaluator().AuthorizeAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.MongoDbFilterJson.Should().BeNull();
    }

    [Fact]
    public async Task ListFilter_ConditionalDeny_BecomesNor_CombinedWithAllowFilter()
    {
        Loaded(
            Rule(AbacEffect.Deny, [Action], mongoFilter: "{\"confidential\":true}"),
            Rule(AbacEffect.Allow, [Action], mongoFilter: "{\"department\":\"hr\"}"));

        var result = await CreateEvaluator().BuildListFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.MongoDbFilterJson.Should().Be("{\"$and\":[{\"department\":\"hr\"},{\"$nor\":[{\"confidential\":true}]}]}");
    }

    [Fact]
    public async Task ListFilter_ConditionalDeny_WithUnrestrictedAllow_IsPureNor()
    {
        Loaded(
            Rule(AbacEffect.Deny, [Action], mongoFilter: "{\"confidential\":true}"),
            Rule(AbacEffect.Deny, [Action], mongoFilter: "{\"archived\":true}"),
            Rule(AbacEffect.Allow, [Action]));

        var result = await CreateEvaluator().BuildListFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.MongoDbFilterJson.Should().Be("{\"$nor\":[{\"confidential\":true},{\"archived\":true}]}");
    }

    [Fact]
    public async Task ListFilter_UnexpressibleDeny_FailsClosed()
    {
        Loaded(
            Rule(AbacEffect.Deny, [Action], hasResourceConditions: true),
            Rule(AbacEffect.Allow, [Action]));

        (await CreateEvaluator().BuildListFilterAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task ListFilter_UnexpressibleAllow_IsSkipped()
    {
        Loaded(
            Rule(AbacEffect.Allow, [Action], hasResourceConditions: true, ruleId: "unexpressible"),
            Rule(AbacEffect.Allow, [Action], mongoFilter: "{\"department\":\"hr\"}"));

        var result = await CreateEvaluator().BuildListFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.MongoDbFilterJson.Should().Be("{\"department\":\"hr\"}");
    }

    [Fact]
    public async Task ListFilter_OnlyUnexpressibleAllow_Denies()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], hasResourceConditions: true));

        (await CreateEvaluator().BuildListFilterAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);
    }

    // ── Resource overload ─────────────────────────────────────────────────────

    [Fact]
    public async Task Resource_AllowConditionMatches_Allows_OtherwiseDenies()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], resourceConditions: Cond("department", "$eq", "hr")));
        var evaluator = CreateEvaluator();

        (await evaluator.AuthorizeAsync(OrgId, Action, new Dictionary<string, object?> { ["department"] = "HR" }))
            .Decision.Should().Be(AbacDecision.Allow);
        (await evaluator.AuthorizeAsync(OrgId, Action, new Dictionary<string, object?> { ["department"] = "ops" }))
            .Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task Resource_ConditionalDenyMatches_Denies()
    {
        Loaded(
            Rule(AbacEffect.Deny, [Action], resourceConditions: Cond("confidential", "$eq", true)),
            Rule(AbacEffect.Allow, [Action]));
        var evaluator = CreateEvaluator();

        (await evaluator.AuthorizeAsync(OrgId, Action, new Dictionary<string, object?> { ["confidential"] = true }))
            .Decision.Should().Be(AbacDecision.Deny);
        (await evaluator.AuthorizeAsync(OrgId, Action, new Dictionary<string, object?> { ["confidential"] = false }))
            .Decision.Should().Be(AbacDecision.Allow);
    }

    [Fact]
    public async Task Resource_DotPath_IsResolved()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], resourceConditions: Cond("address.country", "$in", new List<object> { "DE", "AT" })));

        var resource = new Dictionary<string, object?>
        {
            ["address"] = new Dictionary<string, object?> { ["country"] = "DE" }
        };

        (await CreateEvaluator().AuthorizeAsync(OrgId, Action, resource)).Decision.Should().Be(AbacDecision.Allow);
    }

    [Fact]
    public async Task Resource_DenyWithoutParsableConditions_FailsClosed()
    {
        Loaded(
            Rule(AbacEffect.Deny, [Action], hasResourceConditions: true),
            Rule(AbacEffect.Allow, [Action]));

        (await CreateEvaluator().AuthorizeAsync(OrgId, Action, new Dictionary<string, object?> { ["x"] = 1 }))
            .Decision.Should().Be(AbacDecision.Deny);
    }

    // ── Subject variables ─────────────────────────────────────────────────────

    [Fact]
    public async Task SubjectVariable_SingleValued_IsSubstitutedInMongoFilter()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], mongoFilter: "{\"ownerId\":{\"$eq\":\"{subject.user.id}\"}}"));

        var result = await CreateEvaluator().BuildListFilterAsync(OrgId, Action);

        result.MongoDbFilterJson.Should().Be("{\"ownerId\":{\"$eq\":\"11111111-1111-1111-1111-111111111111\"}}");
    }

    [Fact]
    public async Task SubjectVariable_MultiValued_EqBecomesIn_InMongoFilter()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], mongoFilter: "{\"teamId\":{\"$eq\":\"{subject.teams.id}\"}}"));

        var result = await CreateEvaluator().BuildListFilterAsync(OrgId, Action);

        result.MongoDbFilterJson.Should().Be($"{{\"teamId\":{{\"$in\":[\"{TeamA}\",\"{TeamB}\"]}}}}");
    }

    [Fact]
    public async Task SubjectVariable_MultiValued_TermBecomesTerms_InSearchFilter()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], searchFilter: "{\"term\":{\"teamName\":\"{subject.teams.name}\"}}"));

        var result = await CreateEvaluator().BuildSearchFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.SearchFilterJson.Should().Contain("{\"terms\":{\"teamName\":[\"Sales\",\"Support\"]}}");
    }

    [Fact]
    public async Task SubjectVariable_EvaluatedInProcess_ForResourceOverload()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], resourceConditions: Cond("teamId", "$eq", "{subject.teams.id}")));
        var evaluator = CreateEvaluator();

        (await evaluator.AuthorizeAsync(OrgId, Action, new Dictionary<string, object?> { ["teamId"] = TeamB }))
            .Decision.Should().Be(AbacDecision.Allow);
        (await evaluator.AuthorizeAsync(OrgId, Action, new Dictionary<string, object?> { ["teamId"] = Guid.NewGuid() }))
            .Decision.Should().Be(AbacDecision.Deny);
    }

    // ── L1 cache ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task L1Cache_SameSetId_ReturnsCachedResult()
    {
        var policies = new List<CompiledPolicy> { Policy(OrgId, "p1", Rule(AbacEffect.Allow, [Action])) };
        Snapshot(new AbacPolicySnapshot { Status = AbacPolicySnapshotStatus.Loaded, SetId = "set-1", Policies = policies });
        var evaluator = CreateEvaluator(withL1: true);

        (await evaluator.AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Allow);

        // Mutate the set content without changing the set id → cached decision is returned.
        policies[0].CompiledRules[0].Effect = AbacEffect.Deny;
        (await evaluator.AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Allow);

        // New set id → implicit invalidation.
        Snapshot(new AbacPolicySnapshot { Status = AbacPolicySnapshotStatus.Loaded, SetId = "set-2", Policies = policies });
        (await evaluator.AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task L1Cache_ResourceDecisions_AreNotCached()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], resourceConditions: Cond("department", "$eq", "hr")));
        var evaluator = CreateEvaluator(withL1: true);

        (await evaluator.AuthorizeAsync(OrgId, Action, new Dictionary<string, object?> { ["department"] = "hr" }))
            .Decision.Should().Be(AbacDecision.Allow);
        (await evaluator.AuthorizeAsync(OrgId, Action, new Dictionary<string, object?> { ["department"] = "ops" }))
            .Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task L1Cache_DifferentSubjects_HaveDifferentEntries()
    {
        Loaded(Rule(AbacEffect.Allow, [Action], subjectConditions: Cond("roles", "$eq", "admin")));
        var evaluator = CreateEvaluator(withL1: true);

        (await evaluator.AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Deny);

        _subjectFactory.Setup(f => f.BuildSubject()).Returns(Subject(OrgId, ["admin"]));
        (await evaluator.AuthorizeAsync(OrgId, Action)).Decision.Should().Be(AbacDecision.Allow);
    }

    [Fact]
    public void SubjectHash_IsOrderIndependent()
    {
        var a = Subject(OrgId, ["a", "b"], (TeamA, "Sales"), (TeamB, "Support"));
        var b = Subject(OrgId, ["b", "a"], (TeamB, "Support"), (TeamA, "Sales"));

        AbacEvaluator.ComputeSubjectHash(a).Should().Be(AbacEvaluator.ComputeSubjectHash(b));
        AbacEvaluator.ComputeSubjectHash(a).Should().NotBe(AbacEvaluator.ComputeSubjectHash(Subject(OrgId, ["a"])));
    }

    // ── HasDenyRulesForActionAsync ────────────────────────────────────────────

    [Fact]
    public async Task HasDenyRules_Unavailable_True()
    {
        Snapshot(AbacPolicySnapshot.Unavailable);
        (await CreateEvaluator().HasDenyRulesForActionAsync(OrgId, Action)).Should().BeTrue();
    }

    [Fact]
    public async Task HasDenyRules_MatchingDeny_True_OtherAction_False()
    {
        Loaded(
            Rule(AbacEffect.Deny, ["oxs:contact:*"], mongoFilter: "{\"x\":1}", subjectConditions: Cond("roles", "$eq", "nobody")),
            Rule(AbacEffect.Allow, ["*"]));
        var evaluator = CreateEvaluator();

        (await evaluator.HasDenyRulesForActionAsync(OrgId, Action)).Should().BeTrue("subject conditions are ignored");
        (await evaluator.HasDenyRulesForActionAsync(OrgId, "oxs:invoice:read")).Should().BeFalse();
    }

    [Fact]
    public async Task HasDenyRules_OnlyAllow_False()
    {
        Loaded(Rule(AbacEffect.Allow, [Action]));
        (await CreateEvaluator().HasDenyRulesForActionAsync(OrgId, Action)).Should().BeFalse();
    }

    [Fact]
    public async Task HasDenyRules_ConfirmedEmpty_DependsOnRolloutFlag()
    {
        Snapshot(new AbacPolicySnapshot { Status = AbacPolicySnapshotStatus.ConfirmedEmpty, AllowAllWhenEmpty = true });
        (await CreateEvaluator().HasDenyRulesForActionAsync(OrgId, Action)).Should().BeFalse();

        Snapshot(new AbacPolicySnapshot { Status = AbacPolicySnapshotStatus.ConfirmedEmpty, AllowAllWhenEmpty = false });
        (await CreateEvaluator().HasDenyRulesForActionAsync(OrgId, Action)).Should().BeTrue();
    }
}

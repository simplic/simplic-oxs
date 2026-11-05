using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Simplic.OxS.ABAC.Compiled;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Subject;
using Simplic.OxS.Server.Abac;
using Xunit;

namespace Simplic.OxS.Server.Test.Abac;

/// <summary>
/// Unit tests for <see cref="AbacEvaluator"/>.
/// Mocks <see cref="IAbacPolicyReader"/> and <see cref="IAbacSubjectFactory"/>
/// to keep tests free of Redis and HTTP context dependencies.
/// </summary>
public class AbacEvaluatorTest
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private const string Action = "oxs:document:read";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AbacSubject DefaultSubject(string[]? roles = null) => new()
    {
        OrganizationId = OrgId,
        User = new AbacSubjectUser { Id = Guid.NewGuid(), Email = "user@example.com" },
        Roles = roles ?? new[] { "viewer" },
        Teams = new List<AbacTeam>()
    };

    private static CompiledRule AllowRule(
        string[]? actions = null,
        bool hasSubjectConditions = false,
        IDictionary<string, object>? subjectRawJson = null,
        string? mongoFilter = null) => new()
    {
        RuleId = "r-allow",
        Effect = AbacEffect.Allow,
        ActionPatterns = actions ?? new[] { Action },
        SubjectConditions = new RuleConditions { RawJson = subjectRawJson },
        ResourceConditions = new RuleConditions(),
        MongoDbFilterJson = mongoFilter,
        Metadata = new RuleMetadata { HasSubjectConditions = hasSubjectConditions, HasResourceConditions = mongoFilter != null }
    };

    private static CompiledRule DenyRule(
        string[]? actions = null,
        bool hasSubjectConditions = false,
        IDictionary<string, object>? subjectRawJson = null) => new()
    {
        RuleId = "r-deny",
        Effect = AbacEffect.Deny,
        ActionPatterns = actions ?? new[] { Action },
        SubjectConditions = new RuleConditions { RawJson = subjectRawJson },
        ResourceConditions = new RuleConditions(),
        Metadata = new RuleMetadata { HasSubjectConditions = hasSubjectConditions }
    };

    private static CompiledPolicy PolicyWith(params CompiledRule[] rules) => new()
    {
        PolicyId = "test-policy",
        OrganizationId = OrgId,
        CompilerVersion = "1",
        SetId = "set-1",
        CompiledRules = rules.ToList()
    };

    private static AbacEvaluator BuildEvaluator(
        IAbacSubjectFactory subjectFactory,
        IAbacPolicyReader policyReader) =>
        new(policyReader, subjectFactory, Mock.Of<ILogger<AbacEvaluator>>());

    private static (AbacEvaluator evaluator, Mock<IAbacPolicyReader> readerMock) BuildWithReader(
        AbacSubject? subject,
        IReadOnlyList<CompiledPolicy> policies)
    {
        var factoryMock = new Mock<IAbacSubjectFactory>();
        factoryMock.Setup(f => f.BuildSubjectAsync(It.IsAny<CancellationToken>())).ReturnsAsync(subject);

        var readerMock = new Mock<IAbacPolicyReader>();
        readerMock.Setup(r => r.GetSnapshotAsync(OrgId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new AbacPolicySnapshot
                  {
                      Status = AbacPolicySnapshotStatus.Loaded,
                      Source = AbacPolicySource.Redis,
                      SetId = "set-1",
                      Policies = policies
                  });

        return (BuildEvaluator(factoryMock.Object, readerMock.Object), readerMock);
    }

    // ── No subject (unauthenticated) ──────────────────────────────────────────

    [Fact]
    public async Task AuthorizeAsync_NoSubject_Denies()
    {
        var (evaluator, _) = BuildWithReader(null, new List<CompiledPolicy>());

        var result = await evaluator.AuthorizeAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
    }

    // ── No policies (empty active set) ────────────────────────────────────────

    [Fact]
    public async Task AuthorizeAsync_NoPolicies_Denies()
    {
        var (evaluator, _) = BuildWithReader(DefaultSubject(), new List<CompiledPolicy>());

        var result = await evaluator.AuthorizeAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
    }

    // ── Version mismatch ──────────────────────────────────────────────────────

    [Fact]
    public async Task AuthorizeAsync_VersionMismatch_DeniesWithFlag()
    {
        var policy = PolicyWith(AllowRule());
        policy.CompilerVersion = "0"; // wrong version

        var (evaluator, _) = BuildWithReader(DefaultSubject(), new List<CompiledPolicy> { policy });

        var result = await evaluator.AuthorizeAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
        result.IsVersionMismatch.Should().BeTrue();
    }

    // ── Allow without conditions (unrestricted) ───────────────────────────────

    [Fact]
    public async Task AuthorizeAsync_UnconditionalAllow_Allows()
    {
        var (evaluator, _) = BuildWithReader(DefaultSubject(), new List<CompiledPolicy> { PolicyWith(AllowRule()) });

        var result = await evaluator.AuthorizeAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.MongoDbFilterJson.Should().BeNull();
    }

    // ── Deny-first ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AuthorizeAsync_DenyBeforeAllow_Denies()
    {
        var (evaluator, _) = BuildWithReader(
            DefaultSubject(),
            new List<CompiledPolicy> { PolicyWith(DenyRule(), AllowRule()) });

        var result = await evaluator.AuthorizeAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
    }

    // ── Allow with subject condition ──────────────────────────────────────────

    [Fact]
    public async Task AuthorizeAsync_AllowWithSubjectCondition_AllowsWhenMatches()
    {
        var subject = DefaultSubject(roles: new[] { "admin" });
        var subjectConditions = new Dictionary<string, object>
        {
            ["roles"] = new Dictionary<string, object> { ["$eq"] = "admin" }
        };

        var (evaluator, _) = BuildWithReader(
            subject,
            new List<CompiledPolicy> { PolicyWith(AllowRule(hasSubjectConditions: true, subjectRawJson: subjectConditions)) });

        var result = await evaluator.AuthorizeAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
    }

    [Fact]
    public async Task AuthorizeAsync_AllowWithSubjectCondition_DeniesWhenNotMatches()
    {
        var subject = DefaultSubject(roles: new[] { "viewer" });
        var subjectConditions = new Dictionary<string, object>
        {
            ["roles"] = new Dictionary<string, object> { ["$eq"] = "admin" }
        };

        var (evaluator, _) = BuildWithReader(
            subject,
            new List<CompiledPolicy> { PolicyWith(AllowRule(hasSubjectConditions: true, subjectRawJson: subjectConditions)) });

        var result = await evaluator.AuthorizeAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
    }

    // ── Filter building ───────────────────────────────────────────────────────

    [Fact]
    public async Task BuildListFilterAsync_AllowWithMongoFilter_ReturnsFilterJson()
    {
        const string filterJson = "{\"department\":{\"$eq\":\"hr\"}}";
        var (evaluator, _) = BuildWithReader(
            DefaultSubject(),
            new List<CompiledPolicy> { PolicyWith(AllowRule(mongoFilter: filterJson)) });

        var result = await evaluator.BuildListFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.MongoDbFilterJson.Should().Be(filterJson);
    }

    [Fact]
    public async Task BuildListFilterAsync_MultipleAllowFilters_CombinesWithOr()
    {
        const string filter1 = "{\"department\":{\"$eq\":\"hr\"}}";
        const string filter2 = "{\"department\":{\"$eq\":\"ops\"}}";

        var (evaluator, _) = BuildWithReader(
            DefaultSubject(),
            new List<CompiledPolicy> { PolicyWith(AllowRule(mongoFilter: filter1), AllowRule(mongoFilter: filter2)) });

        var result = await evaluator.BuildListFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.MongoDbFilterJson.Should().StartWith("{\"$or\":[");
        result.MongoDbFilterJson.Should().Contain(filter1);
        result.MongoDbFilterJson.Should().Contain(filter2);
    }

    // ── Wildcard action matching ───────────────────────────────────────────────

    [Fact]
    public async Task AuthorizeAsync_WildcardAction_MatchesPrefixed()
    {
        var (evaluator, _) = BuildWithReader(
            DefaultSubject(),
            new List<CompiledPolicy> { PolicyWith(AllowRule(actions: new[] { "oxs:document:*" })) });

        var result = await evaluator.AuthorizeAsync(OrgId, "oxs:document:read");

        result.Decision.Should().Be(AbacDecision.Allow);
    }

    [Fact]
    public async Task AuthorizeAsync_WildcardAction_DoesNotMatchUnrelated()
    {
        var (evaluator, _) = BuildWithReader(
            DefaultSubject(),
            new List<CompiledPolicy> { PolicyWith(AllowRule(actions: new[] { "oxs:document:*" })) });

        var result = await evaluator.AuthorizeAsync(OrgId, "oxs:invoice:read");

        result.Decision.Should().Be(AbacDecision.Deny);
    }
}

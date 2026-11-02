using System.Text.Json;
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
/// Unit tests for <see cref="AbacEvaluator.BuildSearchFilterAsync"/> (Elasticsearch path).
/// Mocks the policy reader and subject factory — no Redis or HTTP context.
/// </summary>
public class AbacEvaluatorSearchTest
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private const string Action = "oxs:document:search";

    private static AbacSubject DefaultSubject() => new()
    {
        OrganizationId = OrgId,
        User = new AbacSubjectUser { Id = Guid.NewGuid(), Email = "user@example.com" },
        Roles = ["viewer"],
        Teams = []
    };

    private static CompiledRule Rule(
        AbacEffect effect,
        bool hasResourceConditions,
        string? searchFilter) => new()
    {
        RuleId = effect == AbacEffect.Deny ? "r-deny" : "r-allow",
        Effect = effect,
        ActionPatterns = [Action],
        SubjectConditions = new RuleConditions(),
        ResourceConditions = new RuleConditions(),
        SearchFilter = searchFilter,
        Metadata = new RuleMetadata { HasResourceConditions = hasResourceConditions }
    };

    private static CompiledPolicy PolicyWith(params CompiledRule[] rules) => new()
    {
        PolicyId = "test-policy",
        OrganizationId = OrgId,
        CompilerVersion = "1",
        SetId = "set-1",
        CompiledRules = rules.ToList()
    };

    private static AbacEvaluator Build(IReadOnlyList<CompiledPolicy> policies, AbacSubject? subject)
    {
        var factory = new Mock<IAbacSubjectFactory>();
        factory.Setup(f => f.BuildSubject()).Returns(subject);

        var reader = new Mock<IAbacPolicyReader>();
        reader.Setup(r => r.GetSnapshotAsync(OrgId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new AbacPolicySnapshot
              {
                  Status = AbacPolicySnapshotStatus.Loaded,
                  Source = AbacPolicySource.Redis,
                  SetId = "set-1",
                  Policies = policies
              });

        return new AbacEvaluator(reader.Object, factory.Object, Mock.Of<ILogger<AbacEvaluator>>());
    }

    [Fact]
    public async Task UnrestrictedAllow_NoFilter()
    {
        var evaluator = Build([PolicyWith(Rule(AbacEffect.Allow, false, null))], DefaultSubject());

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        result.SearchFilterJson.Should().BeNull();
    }

    [Fact]
    public async Task NoAllow_Denies()
    {
        var evaluator = Build([PolicyWith(Rule(AbacEffect.Deny, true, "{\"term\":{\"status\":\"archived\"}}"))], DefaultSubject());

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task BlanketDeny_Denies()
    {
        var evaluator = Build(
            [PolicyWith(Rule(AbacEffect.Allow, false, null), Rule(AbacEffect.Deny, false, null))],
            DefaultSubject());

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task AllowWithResourceFilter_WrapsInShould()
    {
        const string allow = "{\"term\":{\"department\":\"finance\"}}";
        var evaluator = Build([PolicyWith(Rule(AbacEffect.Allow, true, allow))], DefaultSubject());

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        var root = JsonDocument.Parse(result.SearchFilterJson!).RootElement;
        var filter = root.GetProperty("bool").GetProperty("filter");
        filter.GetArrayLength().Should().Be(1);
        filter[0].GetProperty("term").GetProperty("department").GetString().Should().Be("finance");
    }

    [Fact]
    public async Task MultipleAllowFilters_CombineWithShould()
    {
        const string a = "{\"term\":{\"department\":\"finance\"}}";
        const string b = "{\"term\":{\"owner\":\"u1\"}}";
        var evaluator = Build([PolicyWith(Rule(AbacEffect.Allow, true, a), Rule(AbacEffect.Allow, true, b))], DefaultSubject());

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        var root = JsonDocument.Parse(result.SearchFilterJson!).RootElement;
        var shouldGroup = root.GetProperty("bool").GetProperty("filter")[0];
        var should = shouldGroup.GetProperty("bool").GetProperty("should");
        should.GetArrayLength().Should().Be(2);
        shouldGroup.GetProperty("bool").GetProperty("minimum_should_match").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task AllowAllPlusDeny_ProducesMustNotOnly()
    {
        const string deny = "{\"term\":{\"status\":\"archived\"}}";
        var evaluator = Build(
            [PolicyWith(Rule(AbacEffect.Allow, false, null), Rule(AbacEffect.Deny, true, deny))],
            DefaultSubject());

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Allow);
        var root = JsonDocument.Parse(result.SearchFilterJson!).RootElement;
        root.GetProperty("bool").TryGetProperty("filter", out _).Should().BeFalse();
        root.GetProperty("bool").GetProperty("must_not")[0]
            .GetProperty("term").GetProperty("status").GetString().Should().Be("archived");
    }

    [Fact]
    public async Task AllowAndDeny_ProducesFilterAndMustNot()
    {
        const string allow = "{\"term\":{\"department\":\"finance\"}}";
        const string deny = "{\"term\":{\"status\":\"archived\"}}";
        var evaluator = Build(
            [PolicyWith(Rule(AbacEffect.Allow, true, allow), Rule(AbacEffect.Deny, true, deny))],
            DefaultSubject());

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        var root = JsonDocument.Parse(result.SearchFilterJson!).RootElement;
        root.GetProperty("bool").GetProperty("filter").GetArrayLength().Should().Be(1);
        root.GetProperty("bool").GetProperty("must_not").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task DenyWithUnexpressibleResource_FailsClosed()
    {
        // HasResourceConditions but SearchFilter null → cannot express must_not → fail closed.
        var evaluator = Build(
            [PolicyWith(Rule(AbacEffect.Allow, false, null), Rule(AbacEffect.Deny, true, null))],
            DefaultSubject());

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task AllowWithUnexpressibleResource_IgnoredThenDenies()
    {
        // Only allow rule is unexpressible → nothing searchable granted → fail closed.
        var evaluator = Build([PolicyWith(Rule(AbacEffect.Allow, true, null))], DefaultSubject());

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task NoSubject_Denies()
    {
        var evaluator = Build([PolicyWith(Rule(AbacEffect.Allow, false, null))], null);

        var result = await evaluator.BuildSearchFilterAsync(OrgId, Action);

        result.Decision.Should().Be(AbacDecision.Deny);
    }
}

using FluentAssertions;
using Simplic.OxS.ABAC.Runtime;
using Xunit;
using static Simplic.OxS.Server.Test.Abac.AbacTestData;

namespace Simplic.OxS.Server.Test.Abac;

/// <summary>Tests for <see cref="AbacSubjectVariableResolver"/> and <see cref="AbacResourceConditionEvaluator"/>.</summary>
public class AbacSubjectVariableResolverTest
{
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid TeamA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TeamB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Simplic.OxS.ABAC.Subject.AbacSubject Subj = Subject(OrgId, ["viewer", "editor"], (TeamA, "Sales"), (TeamB, "Support"));

    [Theory]
    [InlineData("{\"teamId\":\"{subject.teams.id}\"}", "{\"teamId\":{\"$in\":[\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\"]}}")]
    [InlineData("{\"role\":{\"$ne\":\"{subject.roles}\"}}", "{\"role\":{\"$nin\":[\"viewer\",\"editor\"]}}")]
    [InlineData("{\"email\":\"{subject.user.email}\"}", "{\"email\":\"user@example.com\"}")]
    [InlineData("{\"role\":{\"$in\":[\"admin\",\"{subject.roles}\"]}}", "{\"role\":{\"$in\":[\"admin\",\"viewer\",\"editor\"]}}")]
    [InlineData("{\"x\":1}", "{\"x\":1}")]
    public void SubstituteFilterJson_Mongo(string input, string expected)
        => AbacSubjectVariableResolver.SubstituteFilterJson(input, Subj, AbacFilterDialect.MongoDb).Should().Be(expected);

    [Fact]
    public void SubstituteFilterJson_Elasticsearch_TermValueForm_BecomesTerms()
        => AbacSubjectVariableResolver.SubstituteFilterJson("{\"term\":{\"role\":{\"value\":\"{subject.roles}\"}}}", Subj, AbacFilterDialect.Elasticsearch)
            .Should().Be("{\"terms\":{\"role\":[\"viewer\",\"editor\"]}}");

    [Fact]
    public void ResourceEvaluator_MissingField_PositiveFalse_NegatedTrue()
    {
        var resource = new Dictionary<string, object?> { ["other"] = 1 };

        AbacResourceConditionEvaluator.Evaluate(ConditionNodeParser.Parse(Cond("department", "$eq", "hr")), resource).Should().BeFalse();
        AbacResourceConditionEvaluator.Evaluate(ConditionNodeParser.Parse(Cond("department", "$ne", "hr")), resource).Should().BeTrue();
    }

    [Fact]
    public void ResourceEvaluator_ArrayField_MatchesAnyElement_AndContains()
    {
        var resource = new Dictionary<string, object?> { ["tags"] = new[] { "a", "b" } };

        AbacResourceConditionEvaluator.Evaluate(ConditionNodeParser.Parse(Cond("tags", "$eq", "b")), resource).Should().BeTrue();
        AbacResourceConditionEvaluator.Evaluate(ConditionNodeParser.Parse(Cond("tags", "$contains", "a")), resource).Should().BeTrue();
        AbacResourceConditionEvaluator.Evaluate(ConditionNodeParser.Parse(Cond("tags", "$ncontains", "a")), resource).Should().BeFalse();
    }

    [Fact]
    public void ResourceEvaluator_NumericComparison()
    {
        var resource = new Dictionary<string, object?> { ["amount"] = 150m };

        AbacResourceConditionEvaluator.Evaluate(ConditionNodeParser.Parse(Cond("amount", "$gt", 100)), resource).Should().BeTrue();
        AbacResourceConditionEvaluator.Evaluate(ConditionNodeParser.Parse(Cond("amount", "$lte", 100)), resource).Should().BeFalse();
    }

    [Fact]
    public void ResourceEvaluator_SubjectVariable_NotEqualMultiValued()
    {
        var node = ConditionNodeParser.Parse(Cond("ownerTeam", "$ne", "{subject.teams.name}"));

        AbacResourceConditionEvaluator.Evaluate(node, new Dictionary<string, object?> { ["ownerTeam"] = "Sales" }, Subj).Should().BeFalse();
        AbacResourceConditionEvaluator.Evaluate(node, new Dictionary<string, object?> { ["ownerTeam"] = "HR" }, Subj).Should().BeTrue();
    }
}

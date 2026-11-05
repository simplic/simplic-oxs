using FluentAssertions;
using Simplic.OxS.ABAC.Ast;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Subject;
using Xunit;

namespace Simplic.OxS.Server.Test.Abac;

/// <summary>
/// Unit tests for <see cref="AbacConditionEvaluator"/> and <see cref="ConditionNodeParser"/>.
/// These cover pure in-process logic only — no Redis, no HTTP context.
/// </summary>
public class AbacConditionEvaluatorTest
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AbacSubject BuildSubject(
        string email = "alice@example.com",
        string[]? roles = null,
        (Guid id, string name)[]? teams = null)
    {
        return new AbacSubject
        {
            OrganizationId = Guid.NewGuid(),
            User = new AbacSubjectUser { Id = Guid.NewGuid(), Email = email },
            Roles = roles ?? ["viewer"],
            Teams = teams?.Select(t => new AbacTeam { Id = t.id, Name = t.name }).ToList()
                    ?? []
        };
    }

    // ── ConditionNodeParser ───────────────────────────────────────────────────

    [Fact]
    public void Parse_NullOrEmpty_ReturnsNull()
    {
        ConditionNodeParser.Parse(null).Should().BeNull();
        ConditionNodeParser.Parse(new Dictionary<string, object>()).Should().BeNull();
    }

    [Fact]
    public void Parse_SingleComparison_BuildsComparisonNode()
    {
        var raw = new Dictionary<string, object>
        {
            ["roles"] = new Dictionary<string, object> { ["$in"] = new List<object> { "admin", "manager" } }
        };

        var node = ConditionNodeParser.Parse(raw);

        node.Should().NotBeNull();
        node!.NodeType.Should().Be(ConditionNodeType.Comparison);
        node.Field.Should().Be("roles");
        node.Operator.Should().Be("$in");
    }

    [Fact]
    public void Parse_MultipleComparisons_WrapsInImplicitAnd()
    {
        var raw = new Dictionary<string, object>
        {
            ["roles"] = new Dictionary<string, object> { ["$eq"] = "admin" },
            ["user.email"] = new Dictionary<string, object> { ["$eq"] = "alice@example.com" }
        };

        var node = ConditionNodeParser.Parse(raw);

        node.Should().NotBeNull();
        node!.NodeType.Should().Be(ConditionNodeType.And);
        node.Children.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_ExplicitOr_BuildsOrNode()
    {
        var orItems = new List<object>
        {
            new Dictionary<string, object> { ["roles"] = new Dictionary<string, object> { ["$eq"] = "admin" } },
            new Dictionary<string, object> { ["roles"] = new Dictionary<string, object> { ["$eq"] = "manager" } }
        };

        var raw = new Dictionary<string, object> { ["$or"] = orItems };

        var node = ConditionNodeParser.Parse(raw);

        node.Should().NotBeNull();
        node!.NodeType.Should().Be(ConditionNodeType.Or);
        node.Children.Should().HaveCount(2);
    }

    // ── AbacConditionEvaluator — $eq ──────────────────────────────────────────

    [Fact]
    public void Evaluate_NullNode_ReturnsTrue()
    {
        AbacConditionEvaluator.Evaluate(null, BuildSubject()).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_EqRole_MatchesExact()
    {
        var subject = BuildSubject(roles: ["admin"]);
        var node = new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = "roles",
            Operator = "$eq",
            Value = "admin"
        };

        AbacConditionEvaluator.Evaluate(node, subject).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_EqRole_DoesNotMatchWrongRole()
    {
        var subject = BuildSubject(roles: ["viewer"]);
        var node = new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = "roles",
            Operator = "$eq",
            Value = "admin"
        };

        AbacConditionEvaluator.Evaluate(node, subject).Should().BeFalse();
    }

    // ── $in ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_InRoles_MatchesOneOfList()
    {
        var subject = BuildSubject(roles: ["manager"]);
        var node = new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = "roles",
            Operator = "$in",
            Value = new List<object?> { "admin", "manager" }
        };

        AbacConditionEvaluator.Evaluate(node, subject).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_InRoles_NoMatch()
    {
        var subject = BuildSubject(roles: ["viewer"]);
        var node = new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = "roles",
            Operator = "$in",
            Value = new List<object?> { "admin", "manager" }
        };

        AbacConditionEvaluator.Evaluate(node, subject).Should().BeFalse();
    }

    // ── teams.name ────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_TeamsName_MatchesTeamMember()
    {
        var subject = BuildSubject(teams: [(Guid.NewGuid(), "hr"), (Guid.NewGuid(), "ops")]);
        var node = new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = "teams.name",
            Operator = "$eq",
            Value = "hr"
        };

        AbacConditionEvaluator.Evaluate(node, subject).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_TeamsName_NoMatch()
    {
        var subject = BuildSubject(teams: [(Guid.NewGuid(), "ops")]);
        var node = new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = "teams.name",
            Operator = "$eq",
            Value = "hr"
        };

        AbacConditionEvaluator.Evaluate(node, subject).Should().BeFalse();
    }

    // ── Logical combinators ───────────────────────────────────────────────────

    [Fact]
    public void Evaluate_AndNode_RequiresBothTrue()
    {
        var subject = BuildSubject(roles: ["admin"], email: "alice@example.com");

        var andNode = new ConditionNode
        {
            NodeType = ConditionNodeType.And,
            Children =
            [
                new ConditionNode { NodeType = ConditionNodeType.Comparison, Field = "roles", Operator = "$eq", Value = "admin" },
                new ConditionNode { NodeType = ConditionNodeType.Comparison, Field = "user.email", Operator = "$eq", Value = "alice@example.com" }
            ]
        };

        AbacConditionEvaluator.Evaluate(andNode, subject).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_AndNode_FalseWhenOneFails()
    {
        var subject = BuildSubject(roles: ["viewer"], email: "alice@example.com");

        var andNode = new ConditionNode
        {
            NodeType = ConditionNodeType.And,
            Children =
            [
                new ConditionNode { NodeType = ConditionNodeType.Comparison, Field = "roles", Operator = "$eq", Value = "admin" },
                new ConditionNode { NodeType = ConditionNodeType.Comparison, Field = "user.email", Operator = "$eq", Value = "alice@example.com" }
            ]
        };

        AbacConditionEvaluator.Evaluate(andNode, subject).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_OrNode_TrueWhenEitherMatches()
    {
        var subject = BuildSubject(roles: ["manager"]);

        var orNode = new ConditionNode
        {
            NodeType = ConditionNodeType.Or,
            Children =
            [
                new ConditionNode { NodeType = ConditionNodeType.Comparison, Field = "roles", Operator = "$eq", Value = "admin" },
                new ConditionNode { NodeType = ConditionNodeType.Comparison, Field = "roles", Operator = "$eq", Value = "manager" }
            ]
        };

        AbacConditionEvaluator.Evaluate(orNode, subject).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_OrNode_FalseWhenNoneMatch()
    {
        var subject = BuildSubject(roles: ["viewer"]);

        var orNode = new ConditionNode
        {
            NodeType = ConditionNodeType.Or,
            Children =
            [
                new ConditionNode { NodeType = ConditionNodeType.Comparison, Field = "roles", Operator = "$eq", Value = "admin" },
                new ConditionNode { NodeType = ConditionNodeType.Comparison, Field = "roles", Operator = "$eq", Value = "manager" }
            ]
        };

        AbacConditionEvaluator.Evaluate(orNode, subject).Should().BeFalse();
    }

    // ── user.email ────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_UserEmail_CaseInsensitiveMatch()
    {
        var subject = BuildSubject(email: "Alice@Example.COM");
        var node = new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = "user.email",
            Operator = "$eq",
            Value = "alice@example.com"
        };

        AbacConditionEvaluator.Evaluate(node, subject).Should().BeTrue();
    }

    // ── $ne ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Ne_TrueWhenNotEqual()
    {
        var subject = BuildSubject(roles: ["viewer"]);
        var node = new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = "roles",
            Operator = "$ne",
            Value = "admin"
        };

        AbacConditionEvaluator.Evaluate(node, subject).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Ne_FalseWhenEqual()
    {
        var subject = BuildSubject(roles: ["admin"]);
        var node = new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = "roles",
            Operator = "$ne",
            Value = "admin"
        };

        AbacConditionEvaluator.Evaluate(node, subject).Should().BeFalse();
    }
}

using Simplic.OxS.ABAC.Ast;
using Simplic.OxS.ABAC.Subject;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Evaluates a <see cref="ConditionNode"/> AST against an <see cref="AbacSubject"/>.
/// Pure in-process evaluation — no database access, no I/O.
/// Instances are stateless; all members are static.
/// </summary>
public static class AbacConditionEvaluator
{
    /// <summary>
    /// Returns <c>true</c> when the condition tree is satisfied by <paramref name="subject"/>.
    /// A <c>null</c> node is treated as "always true" (no conditions).
    /// </summary>
    public static bool Evaluate(ConditionNode? node, AbacSubject subject)
    {
        if (node == null)
            return true;

        return node.NodeType switch
        {
            ConditionNodeType.And => node.Children.All(c => Evaluate(c, subject)),
            ConditionNodeType.Or => node.Children.Any(c => Evaluate(c, subject)),
            ConditionNodeType.Comparison => EvaluateComparison(node, subject),
            _ => false
        };
    }

    private static bool EvaluateComparison(ConditionNode node, AbacSubject subject)
    {
        if (string.IsNullOrEmpty(node.Field) || string.IsNullOrEmpty(node.Operator))
            return false;

        var subjectValues = AbacSubjectVariableResolver.Resolve(node.Field, subject);

        return node.Operator switch
        {
            "$eq" => MatchAny(subjectValues, v => AbacValueComparer.EqualsCi(v, node.Value)),
            "$ne" => !MatchAny(subjectValues, v => AbacValueComparer.EqualsCi(v, node.Value)),
            "$in" => MatchAny(subjectValues, v => AbacValueComparer.ContainedIn(v, node.Value)),
            "$nin" => !MatchAny(subjectValues, v => AbacValueComparer.ContainedIn(v, node.Value)),
            "$contains" => MatchAny(subjectValues, v => AbacValueComparer.ContainsValue(v, node.Value)),
            "$ncontains" => !MatchAny(subjectValues, v => AbacValueComparer.ContainsValue(v, node.Value)),
            "$gt" => MatchAny(subjectValues, v => AbacValueComparer.Compare(v, node.Value) > 0),
            "$gte" => MatchAny(subjectValues, v => AbacValueComparer.Compare(v, node.Value) >= 0),
            "$lt" => MatchAny(subjectValues, v => AbacValueComparer.Compare(v, node.Value) < 0),
            "$lte" => MatchAny(subjectValues, v => AbacValueComparer.Compare(v, node.Value) <= 0),
            _ => false
        };
    }

    private static bool MatchAny(IReadOnlyList<object?> values, Func<object?, bool> predicate)
        => values.Any(predicate);
}
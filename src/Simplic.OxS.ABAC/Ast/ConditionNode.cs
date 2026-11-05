namespace Simplic.OxS.ABAC.Ast;

/// <summary>
/// Typed AST node produced by the policy compiler from the JSON-first DSL.
/// The compiler (identity service) creates the tree; the runtime evaluator (oxs base)
/// walks it without re-parsing JSON — built in-process per cache-hit, not stored in Redis.
/// </summary>
public class ConditionNode
{
    /// <summary>Discriminates the node role (comparison leaf or logical combinator).</summary>
    public ConditionNodeType NodeType { get; set; }

    // ── Comparison leaf properties (used when NodeType == Comparison) ──────────

    /// <summary>
    /// Dot-notation path into the target object (e.g. <c>teams.name</c>, <c>user.email</c>).
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Comparison operator: <c>$eq</c>, <c>$ne</c>, <c>$in</c>, <c>$nin</c>,
    /// <c>$gt</c>, <c>$gte</c>, <c>$lt</c>, <c>$lte</c>, <c>$contains</c>, <c>$ncontains</c>.
    /// </summary>
    public string? Operator { get; set; }

    /// <summary>
    /// Scalar or array value to compare against.
    /// Must be a JSON-serialisable primitive, string, or <c>IList&lt;object&gt;</c>.
    /// </summary>
    public object? Value { get; set; }

    // ── Logical combinator properties (used when NodeType == And | Or) ─────────

    /// <summary>Child nodes to be combined by the logical operator.</summary>
    public IList<ConditionNode> Children { get; set; } = [];
}

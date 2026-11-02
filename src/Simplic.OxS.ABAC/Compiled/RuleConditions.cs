using Simplic.OxS.ABAC.Ast;

namespace Simplic.OxS.ABAC.Compiled;

/// <summary>
/// Conditions component of a compiled rule, stored in the Redis policy cache.
/// The raw JSON is the only cached representation; the in-process AST is built
/// at evaluation time from <see cref="RawJson"/> (~0.3 ms overhead) and never serialised.
/// </summary>
public class RuleConditions
{
    /// <summary>
    /// Canonical JSON-first representation of the conditions as they appear in the raw policy.
    /// Null or empty dictionary means "always true" (no restriction).
    /// </summary>
    public IDictionary<string, object>? RawJson { get; set; }

    /// <summary>
    /// In-process typed AST built from <see cref="RawJson"/> at evaluation time.
    /// Not serialised to Redis — rebuilt per request from the cached JSON.
    /// </summary>
    public ConditionNode? CompiledAst { get; set; }
}

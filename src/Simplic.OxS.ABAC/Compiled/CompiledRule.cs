using Simplic.OxS.ABAC.Policy;

namespace Simplic.OxS.ABAC.Compiled;

/// <summary>
/// Compiled representation of a single rule, stored in the Redis policy cache.
/// Pre-built backend filters avoid recompilation on every request.
/// </summary>
public class CompiledRule
{
    /// <summary>Matches the source <see cref="AbacRule.RuleId"/>.</summary>
    public string RuleId { get; set; } = string.Empty;

    /// <summary>Whether this compiled rule grants or revokes access.</summary>
    public AbacEffect Effect { get; set; }

    /// <summary>
    /// Action patterns from the source rule (exact strings or prefix-wildcard patterns ending with <c>*</c>).
    /// </summary>
    public string[] ActionPatterns { get; set; } = [];

    /// <summary>Compiled subject conditions (JSON + in-process AST).</summary>
    public RuleConditions SubjectConditions { get; set; } = new();

    /// <summary>Compiled resource conditions (JSON + in-process AST).</summary>
    public RuleConditions ResourceConditions { get; set; } = new();

    /// <summary>
    /// Pre-built MongoDB filter document serialised as canonical JSON.
    /// The runtime converts this to a <c>BsonDocument</c> without re-parsing the policy.
    /// Null when the rule has no resource conditions.
    /// </summary>
    public string? MongoDbFilterJson { get; set; }

    /// <summary>
    /// Pre-built search filter expression serialised as a string.
    /// Null when the rule has no resource conditions or the action does not support search.
    /// </summary>
    public string? SearchFilter { get; set; }

    /// <summary>Optimisation hints for the evaluator.</summary>
    public RuleMetadata Metadata { get; set; } = new();
}

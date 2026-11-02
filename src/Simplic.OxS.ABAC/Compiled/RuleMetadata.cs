namespace Simplic.OxS.ABAC.Compiled;

/// <summary>
/// Optimisation metadata attached to each compiled rule.
/// Allows the evaluator to skip work for rules with no conditions.
/// </summary>
public class RuleMetadata
{
    /// <summary>
    /// <c>true</c> when the rule has non-empty subject conditions.
    /// When <c>false</c>, subject evaluation is skipped (matches any subject).
    /// </summary>
    public bool HasSubjectConditions { get; set; }

    /// <summary>
    /// <c>true</c> when the rule has non-empty resource conditions.
    /// When <c>false</c>, resource evaluation is skipped (matches any resource of this action).
    /// </summary>
    public bool HasResourceConditions { get; set; }

    /// <summary>Subject field paths referenced by this rule (for dependency tracking).</summary>
    public string[] RequiredSubjectFields { get; set; } = [];

    /// <summary>Resource field paths referenced by this rule (for dependency tracking).</summary>
    public string[] RequiredResourceFields { get; set; } = [];
}

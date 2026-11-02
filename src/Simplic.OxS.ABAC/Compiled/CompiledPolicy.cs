namespace Simplic.OxS.ABAC.Compiled;

/// <summary>
/// Compiled and cache-ready representation of an <see cref="Simplic.OxS.ABAC.Policy.AbacPolicy"/>.
/// Written by the compiler (identity service) and read by the runtime evaluator (oxs base).
/// Serialised to Redis as JSON; no serialised expression trees or closures.
/// </summary>
public class CompiledPolicy
{
    /// <summary>Organisation scope — policies are never shared across organisations.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Matches the source policy identifier.</summary>
    public string PolicyId { get; set; } = string.Empty;

    /// <summary>Human-readable description copied from the source policy.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Compiled rules in evaluation order.
    /// Deny rules are listed first to enable short-circuit evaluation.
    /// </summary>
    public IList<CompiledRule> CompiledRules { get; set; } = [];

    /// <summary>UTC timestamp when this policy was compiled.</summary>
    public DateTime CompiledAt { get; set; }

    /// <summary>
    /// Schema version of the engine that produced this compiled policy.
    /// Must match <see cref="Simplic.OxS.ABAC.Cache.AbacEngineVersion.Current"/> on the reader side.
    /// Mismatch → DENY + alert (version-rollout grace period applies).
    /// </summary>
    public string CompilerVersion { get; set; } = string.Empty;

    /// <summary>
    /// Active-set identifier written by the compiler during versioned rollout.
    /// Readers use the pointer at <c>abac:policy:{orgId}:active-set</c> to select the correct set.
    /// </summary>
    public string SetId { get; set; } = string.Empty;
}

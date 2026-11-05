namespace Simplic.OxS.ABAC.Policy;

/// <summary>
/// Raw, JSON-first policy as stored in the management database (identity service).
/// Compiled into <see cref="Simplic.OxS.ABAC.Compiled.CompiledPolicy"/> before being cached in Redis.
/// </summary>
public class AbacPolicy
{
    /// <summary>Stable identifier for this policy.</summary>
    public string PolicyId { get; set; } = string.Empty;

    /// <summary>Human-readable description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>One or more rules that make up the policy.</summary>
    public IList<AbacRule> Rules { get; set; } = [];
}

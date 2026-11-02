namespace Simplic.OxS.ABAC.Policy;

/// <summary>
/// Effect of a rule: whether it grants or revokes access.
/// Deny always overrides Allow (deny-first evaluation).
/// </summary>
public enum AbacEffect
{
    /// <summary>Grants access when all conditions are satisfied.</summary>
    Allow,

    /// <summary>Revokes access when all conditions are satisfied. Takes precedence over Allow.</summary>
    Deny
}

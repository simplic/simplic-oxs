namespace Simplic.OxS.ABAC.Policy;

/// <summary>
/// Result of an ABAC authorisation decision.
/// Implicit deny applies when no Allow rule matches.
/// </summary>
public enum AbacDecision
{
    /// <summary>Access is explicitly or implicitly denied.</summary>
    Deny,

    /// <summary>Access is explicitly allowed (at least one Allow rule matched; no Deny rule matched).</summary>
    Allow
}

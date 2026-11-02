namespace Simplic.OxS.ABAC.Validation;

/// <summary>
/// A single validation error produced when the compiler or validator
/// rejects a raw <see cref="Simplic.OxS.ABAC.Policy.AbacPolicy"/>.
/// </summary>
public class AbacPolicyValidationError
{
    /// <summary>Machine-readable error code (e.g. <c>MISSING_RULE_ID</c>, <c>UNKNOWN_OPERATOR</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable description of the problem.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// JSON-pointer path to the offending element within the policy document
    /// (e.g. <c>/rules/0/actions/1</c>).  Null when the error applies to the whole policy.
    /// </summary>
    public string? Path { get; set; }
}

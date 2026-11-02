namespace Simplic.OxS.ABAC.Policy;

/// <summary>
/// A single rule within an <see cref="AbacPolicy"/>.
/// Subject and Resource conditions use the JSON-first DSL
/// (MongoDB-style <c>$eq</c>, <c>$in</c>, <c>$contains</c>, … operators).
/// </summary>
public class AbacRule
{
    /// <summary>Stable identifier for this rule (used in audit logs and cache invalidation).</summary>
    public string RuleId { get; set; } = string.Empty;

    /// <summary>Whether this rule grants or revokes access.</summary>
    public AbacEffect Effect { get; set; }

    /// <summary>
    /// Action patterns this rule applies to.
    /// Format: <c>service:resource:operation</c>.  Trailing <c>*</c> acts as a prefix wildcard.
    /// Examples: <c>oxs:document:read</c>, <c>oxs:document:*</c>, <c>oxs:*</c>.
    /// </summary>
    public IList<string> Actions { get; set; } = [];

    /// <summary>
    /// Subject conditions in the JSON DSL.  Null or empty dictionary means "any subject".
    /// Keys use dot-notation (e.g. <c>teams.name</c>).
    /// </summary>
    public IDictionary<string, object>? Subject { get; set; }

    /// <summary>
    /// Resource conditions in the JSON DSL.  Null or empty dictionary means "any resource of this action".
    /// Keys must map to fields declared in <see cref="Simplic.OxS.ABAC.Contract.IAbacCompileContract.FieldMappings"/>.
    /// </summary>
    public IDictionary<string, object>? Resource { get; set; }
}

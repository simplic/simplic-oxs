namespace Simplic.OxS.ABAC.Sync;

/// <summary>
/// Route constants of the Identity ABAC pull API. The organisation is taken from the
/// request context (<c>OId</c> claim or <c>OrganizationId</c> header for internal calls).
/// </summary>
public static class AbacSyncEndpoints
{
    /// <summary>Service name used for <c>InternalHosts</c> lookup.</summary>
    public const string Service = "identity";

    /// <summary>Controller name.</summary>
    public const string Controller = "AbacPolicySync";

    /// <summary>GET – returns <see cref="AbacOrganizationRuleSet"/> of the current organisation.</summary>
    public const string RuleSet = "rule-set";

    /// <summary>GET – returns <see cref="Mapping.AbacFieldMappingSet"/> of the current organisation.</summary>
    public const string FieldMappings = "field-mappings";
}
namespace Simplic.OxS.ABAC;

/// <summary>ABAC actions protecting the ABAC admin API itself (§10.3).</summary>
public static class AbacAdminActions
{
    /// <summary>Read policies / audit.</summary>
    public const string PolicyRead = "oxs:abac-policy:read";
    /// <summary>Create/update/delete policies.</summary>
    public const string PolicyWrite = "oxs:abac-policy:write";
    /// <summary>Compile and activate a rule set.</summary>
    public const string PolicyActivate = "oxs:abac-policy:activate";
    /// <summary>Read field mappings.</summary>
    public const string MappingRead = "oxs:abac-mapping:read";
    /// <summary>Change field mappings.</summary>
    public const string MappingWrite = "oxs:abac-mapping:write";

    /// <summary>Roles (claim <c>Roles</c>, case-insensitive) allowed to use the admin API.</summary>
    public static readonly IReadOnlyList<string> AdminRoles = ["owner", "admin"];

    /// <summary>Role allowed to change the rollout switch.</summary>
    public const string OwnerRole = "owner";
}
namespace Simplic.OxS.ABAC.Cache;

/// <summary>
/// Canonical Redis key templates for all ABAC cache entries.
/// Shared between the compiler (identity service, write side) and the
/// runtime evaluator (oxs base, read side) to guarantee key-space consistency.
/// All keys are organisation-scoped; cross-organisation access is structurally impossible.
/// </summary>
public static class AbacCacheKeys
{
    // ── L1: Decision Cache (TTL 5 min) ──────────────────────────────────────

    /// <summary>
    /// Per-user-per-action authorisation decision (Allow/Deny).
    /// Not used for resource-sensitive decisions (those skip L1 caching).
    /// </summary>
    public static string Decision(Guid orgId, Guid userId, string action)
        => $"abac:decision:{orgId}:{userId}:{action}";

    /// <summary>
    /// Wildcard pattern to invalidate all decision cache entries for an organisation.
    /// Use with Redis SCAN + DEL (not KEYS in production).
    /// </summary>
    public static string OrgDecisionPattern(Guid orgId)
        => $"abac:decision:{orgId}:*";

    /// <summary>
    /// Wildcard pattern to invalidate all decision cache entries for a single user.
    /// Called when user attributes (roles/teams) change.
    /// </summary>
    public static string UserDecisionPattern(Guid orgId, Guid userId)
        => $"abac:decision:{orgId}:{userId}:*";

    // ── L2: Compiled Policy Cache (TTL 60 min) ───────────────────────────────

    /// <summary>
    /// Compiled policy entry for a specific policy within an active-set rollout.
    /// <paramref name="engineVersion"/> should be <see cref="AbacEngineVersion.Current"/>.
    /// <paramref name="setId"/> is the active-set identifier written by the compiler.
    /// </summary>
    public static string CompiledPolicy(Guid orgId, string policyId, string engineVersion, string setId)
        => $"abac:policy:{orgId}:{policyId}:compiled:{engineVersion}:{setId}";

    // ── Active-Set Pointer ───────────────────────────────────────────────────

    /// <summary>
    /// Pointer to the currently active compiled-policy set for an organisation.
    /// Value is the <c>setId</c> (e.g. <c>2026-06-02T08:00Z</c>).
    /// Updated atomically by the compiler after all policies in the new set are written.
    /// </summary>
    public static string ActiveSet(Guid orgId)
        => $"abac:policy:{orgId}:active-set";

    // ── Policy index ─────────────────────────────────────────────────────────

    /// <summary>
    /// JSON-serialised array of all policy IDs in a specific compiled set.
    /// Written by the compiler alongside the compiled policies so that readers
    /// can enumerate all policies without a Redis SCAN.
    /// Value is a <c>string[]</c> (JSON array of policy ID strings).
    /// </summary>
    public static string PolicyIndex(Guid orgId, string engineVersion, string setId)
        => $"abac:policy:{orgId}:index:{engineVersion}:{setId}";

    /// <summary>
    /// Set flags key. Value is JSON <c>{"isConfirmedEmpty":bool,"allowAllWhenEmpty":bool}</c>.
    /// Written by the compiler (and the service sync) next to the index.
    /// </summary>
    public static string Flags(Guid orgId, string setId)
        => $"abac:policy:{orgId}:flags:{setId}";

    /// <summary>Per-organisation field mapping set (JSON <see cref="Mapping.AbacFieldMappingSet"/>).</summary>
    public static string FieldMappings(Guid orgId)
        => $"abac:mapping:{orgId}";

    // ── Invalidation patterns ────────────────────────────────────────────────

    /// <summary>
    /// Wildcard pattern to delete all compiled policies for an organisation (e.g. on org deletion).
    /// </summary>
    public static string OrgPolicyPattern(Guid orgId)
        => $"abac:policy:{orgId}:*";
}

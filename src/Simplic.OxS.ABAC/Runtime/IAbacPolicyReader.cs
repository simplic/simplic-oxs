using Simplic.OxS.ABAC.Compiled;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Reads the compiled ABAC rule set of an organisation.
/// Resolution order: Redis hot cache → service-local rule store → DENY (§12.6).
/// </summary>
public interface IAbacPolicyReader
{
    /// <summary>
    /// Returns the compiled policies of the organisation's active set, or an empty list when
    /// nothing is available. Prefer <see cref="GetSnapshotAsync"/>, which distinguishes
    /// "confirmed empty" from "unavailable".
    /// </summary>
    Task<IReadOnlyList<CompiledPolicy>> GetPoliciesAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Returns the active rule set together with its load status.</summary>
    Task<AbacPolicySnapshot> GetSnapshotAsync(Guid organizationId, CancellationToken ct = default);
}
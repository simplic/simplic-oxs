namespace Simplic.OxS.Data.MongoDB.Abac;

/// <summary>
/// Durable, service-local store for compiled ABAC rule sets (§12.6, collection <c>abac.compiled-rules</c>).
/// Only the ABAC sync component may write; readers use <see cref="GetActiveAsync"/>.
/// </summary>
public interface IAbacRuleStore
{
    /// <summary>Returns the active rule set of the organization, or <c>null</c> when none is stored.</summary>
    Task<AbacStoredRuleSet?> GetActiveAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Writes the rule set completely, then switches it to active. Afterwards only the active
    /// and the most recent previous set are retained for the organization.
    /// </summary>
    Task SaveActiveAsync(AbacStoredRuleSet ruleSet, CancellationToken ct = default);

    /// <summary>
    /// Returns the active sets (without payload) whose last successful pull is older than
    /// <paramref name="fetchedBeforeUtc"/> — used by the periodic re-sync job.
    /// </summary>
    Task<IReadOnlyList<AbacStoredRuleSet>> GetActiveSetsFetchedBeforeAsync(DateTime fetchedBeforeUtc, CancellationToken ct = default);
}

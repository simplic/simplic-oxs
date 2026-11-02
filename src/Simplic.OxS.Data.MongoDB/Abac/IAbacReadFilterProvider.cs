using Simplic.OxS.ABAC.Runtime;

namespace Simplic.OxS.Data.MongoDB.Abac;

/// <summary>
/// Supplies the ABAC MongoDB read filter for the current request scope (§7.6).
/// Implemented in <c>Simplic.OxS.Server</c> on top of <see cref="IAbacEvaluator.BuildListFilterAsync"/>
/// and the organization of the current request context.
/// </summary>
public interface IAbacReadFilterProvider
{
    /// <summary>
    /// Builds the read filter for <paramref name="action"/> (e.g. <c>oxs:contact:read</c>).
    /// <see cref="AbacDecisionResult.Decision"/> is <c>Deny</c> when the action is denied entirely;
    /// otherwise <see cref="AbacDecisionResult.MongoDbFilterJson"/> holds the filter to AND
    /// (<c>null</c> = unrestricted).
    /// </summary>
    Task<AbacDecisionResult> GetReadFilterAsync(string action, CancellationToken ct = default);
}

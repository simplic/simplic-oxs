using Simplic.OxS.ABAC.Subject;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Builds the ABAC subject for the current request.
/// Implementations take the identity (user ID, organisation ID) from the JWT claims of the
/// current HTTP context and resolve roles and teams server-side from Identity (cached).
/// </summary>
public interface IAbacSubjectFactory
{
    /// <summary>
    /// Builds an <see cref="AbacSubject"/> for the current request. Never throws for missing data:
    /// returns <c>null</c> when no authenticated principal is present or when the subject attributes
    /// cannot be resolved (the evaluator denies requests without a subject — fail closed).
    /// </summary>
    Task<AbacSubject?> BuildSubjectAsync(CancellationToken ct = default);
}

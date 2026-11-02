using Simplic.OxS.ABAC.Subject;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Builds the ABAC subject for the current request.
/// Implementations extract identity data (user ID, organisation ID, roles, teams)
/// from the JWT claims carried by the current HTTP context.
/// </summary>
public interface IAbacSubjectFactory
{
    /// <summary>
    /// Builds an <see cref="AbacSubject"/> from the current request context.
    /// Returns <c>null</c> when no authenticated principal is present
    /// (anonymous requests are always denied by the evaluator).
    /// </summary>
    AbacSubject? BuildSubject();
}

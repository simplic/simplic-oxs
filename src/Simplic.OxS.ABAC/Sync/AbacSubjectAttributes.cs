using Simplic.OxS.ABAC.Subject;

namespace Simplic.OxS.ABAC.Sync;

/// <summary>
/// Live authorization attributes of a user within an organisation, served by Identity
/// (<see cref="AbacSubjectEndpoints"/>) and resolved server-side by the OxS runtime at access time.
/// Roles and teams are deliberately <b>not</b> carried in the JWT (revocation freshness, token size).
/// </summary>
public class AbacSubjectAttributes
{
    /// <summary>Roles held by the user within the organisation.</summary>
    public IList<string> Roles { get; set; } = [];

    /// <summary>Teams the user belongs to within the organisation.</summary>
    public IList<AbacTeam> Teams { get; set; } = [];

    /// <summary>E-mail address of the user (optional; used when the token carries no e-mail claim).</summary>
    public string? Email { get; set; }
}

/// <summary>
/// Route constants of the Identity subject-attribute API:
/// <c>GET /identity-api/{version}/internal/AbacSubject/attributes?userId={guid}&amp;organizationId={guid}</c>.
/// Internal-key authorization, same as <see cref="AbacSyncEndpoints"/>.
/// </summary>
public static class AbacSubjectEndpoints
{
    /// <summary>Controller name.</summary>
    public const string Controller = "AbacSubject";

    /// <summary>
    /// GET – returns <see cref="AbacSubjectAttributes"/>; 404 when the user is unknown or not a member of the
    /// organisation, 503 when the live membership cannot be resolved.
    /// </summary>
    public const string Attributes = "attributes";

    /// <summary>Query parameter carrying the user id.</summary>
    public const string UserIdParameter = "userId";

    /// <summary>Query parameter carrying the organisation id.</summary>
    public const string OrganizationIdParameter = "organizationId";
}

namespace Simplic.OxS.ABAC.Subject;

/// <summary>
/// Represents the authenticated principal extracted from the JWT at request time.
/// Roles and teams are embedded directly in the token (login-time enrichment).
/// </summary>
public class AbacSubject
{
    /// <summary>Organisation the request belongs to.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Authenticated user.</summary>
    public AbacSubjectUser User { get; set; } = new();

    /// <summary>Roles held by the user within this organisation (from JWT claim).</summary>
    public IList<string> Roles { get; set; } = [];

    /// <summary>Teams the user belongs to within this organisation (from JWT claim).</summary>
    public IList<AbacTeam> Teams { get; set; } = [];
}

namespace Simplic.OxS.ABAC.Subject;

/// <summary>
/// Represents the authenticated principal at request time. User id and organisation come from the
/// JWT claims (<c>Id</c>, <c>OId</c>); roles and teams are resolved server-side from Identity
/// (live membership, cached for <c>Abac:Subject:Ttl</c>) and are not part of the token.
/// </summary>
public class AbacSubject
{
    /// <summary>Organisation the request belongs to.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Authenticated user.</summary>
    public AbacSubjectUser User { get; set; } = new();

    /// <summary>Roles held by the user within this organisation (server-side Identity lookup).</summary>
    public IList<string> Roles { get; set; } = [];

    /// <summary>Teams the user belongs to within this organisation (server-side Identity lookup).</summary>
    public IList<AbacTeam> Teams { get; set; } = [];
}

namespace Simplic.OxS.ABAC.Subject;

/// <summary>Identifies the user within a subject.</summary>
public class AbacSubjectUser
{
    /// <summary>User identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Email address (available for condition matching).</summary>
    public string Email { get; set; } = string.Empty;
}

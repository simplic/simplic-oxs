namespace Simplic.OxS.ABAC.Subject;

/// <summary>A team membership embedded in the JWT subject.</summary>
public class AbacTeam
{
    /// <summary>Team identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Team name used in policy conditions (e.g. <c>"teams.name": { "$eq": "hr" }</c>).</summary>
    public string Name { get; set; } = string.Empty;
}

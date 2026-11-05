namespace Simplic.OxS.ABAC.Sync;

/// <summary>
/// Message-bus event published by Identity after a rule set has been compiled and activated
/// for an organisation. The event is only a trigger: consumers always fetch the actual
/// rule set via the pull API (<see cref="AbacSyncEndpoints.RuleSet"/>), never from the event.
/// </summary>
public class AbacPolicySetActivated
{
    /// <summary>Organisation whose active rule set changed.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Identifier of the newly activated set.</summary>
    public string SetId { get; set; } = string.Empty;

    /// <summary>Engine version the set was compiled with.</summary>
    public string EngineVersion { get; set; } = string.Empty;

    /// <summary>UTC activation timestamp.</summary>
    public DateTime ActivatedAt { get; set; }
}
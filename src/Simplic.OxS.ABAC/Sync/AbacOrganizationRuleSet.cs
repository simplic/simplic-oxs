using Simplic.OxS.ABAC.Compiled;

namespace Simplic.OxS.ABAC.Sync;

/// <summary>
/// Full compiled rule set of one organisation as returned by the Identity pull API and as
/// stored in each service's local rule store (collection <c>abac.compiled-rules</c>).
/// </summary>
public class AbacOrganizationRuleSet
{
    /// <summary>Organisation id.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Active set id. Empty when the organisation never activated a set.</summary>
    public string SetId { get; set; } = string.Empty;

    /// <summary>Engine version the set was compiled with.</summary>
    public string EngineVersion { get; set; } = string.Empty;

    /// <summary>UTC activation timestamp.</summary>
    public DateTime ActivatedAt { get; set; }

    /// <summary>
    /// <c>true</c> when Identity confirms that the organisation has no active policies.
    /// Distinguishes "really empty" from "could not load" (which is always DENY).
    /// </summary>
    public bool IsConfirmedEmpty { get; set; }

    /// <summary>
    /// Effective rollout flag (§6.4): when <c>true</c> and <see cref="IsConfirmedEmpty"/> is
    /// <c>true</c>, the runtime allows everything instead of denying. Shipped with the set so
    /// all services decide consistently.
    /// </summary>
    public bool AllowAllWhenEmpty { get; set; }

    /// <summary>Compiled policies of the set.</summary>
    public List<CompiledPolicy> Policies { get; set; } = [];
}
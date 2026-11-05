using Simplic.OxS.ABAC.Compiled;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>Load status of an organisation's rule set.</summary>
public enum AbacPolicySnapshotStatus
{
    /// <summary>Nothing could be loaded (Redis and local store unavailable/expired) → always DENY.</summary>
    Unavailable = 0,

    /// <summary>A rule set with at least one policy was loaded.</summary>
    Loaded = 1,

    /// <summary>The organisation confirmed has no active policies (rollout fallback may apply).</summary>
    ConfirmedEmpty = 2
}

/// <summary>Where a snapshot was loaded from.</summary>
public enum AbacPolicySource
{
    /// <summary>Not loaded.</summary>
    None = 0,
    /// <summary>Redis hot cache.</summary>
    Redis = 1,
    /// <summary>Service-local rule store.</summary>
    LocalStore = 2
}

/// <summary>Result of <see cref="IAbacPolicyReader.GetSnapshotAsync"/>.</summary>
public class AbacPolicySnapshot
{
    /// <summary>Load status.</summary>
    public AbacPolicySnapshotStatus Status { get; init; }

    /// <summary>Source of the data.</summary>
    public AbacPolicySource Source { get; init; }

    /// <summary>Set id (if known).</summary>
    public string? SetId { get; init; }

    /// <summary>Rollout flag shipped with the set.</summary>
    public bool AllowAllWhenEmpty { get; init; }

    /// <summary><c>true</c> when the local store copy is older than the max-age but still within the hard limit.</summary>
    public bool IsStale { get; init; }

    /// <summary>Compiled policies.</summary>
    public IReadOnlyList<CompiledPolicy> Policies { get; init; } = [];

    /// <summary>Unavailable snapshot.</summary>
    public static AbacPolicySnapshot Unavailable { get; } = new() { Status = AbacPolicySnapshotStatus.Unavailable };
}
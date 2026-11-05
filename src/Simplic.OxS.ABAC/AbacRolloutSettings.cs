namespace Simplic.OxS.ABAC;

/// <summary>Rollout fallback configuration (§6.4).</summary>
public static class AbacRolloutSettings
{
    /// <summary>Global configuration key (bool, default <c>false</c>).</summary>
    public const string AllowAllWhenEmptyConfigKey = "Abac:Rollout:AllowAllWhenEmpty";

    /// <summary>Organisation setting key (per-org override via Simplic.OxS.Settings, owner-only writes).</summary>
    public const string AllowAllWhenEmptySettingKey = "abac.rollout.allow-all-when-empty";
}
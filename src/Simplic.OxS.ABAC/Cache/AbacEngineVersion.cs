namespace Simplic.OxS.ABAC.Cache;

/// <summary>
/// Authoritative engine schema version.
/// <para>
/// Increment <see cref="Current"/> on every breaking change to the compiled policy schema
/// (e.g. new required fields in <see cref="Simplic.OxS.ABAC.Compiled.CompiledPolicy"/>
/// or <see cref="Simplic.OxS.ABAC.Compiled.CompiledRule"/>).
/// Old compiled sets remain in Redis during the <see cref="GracePeriodDays"/> window to
/// allow readers to drain gracefully before cleanup.
/// </para>
/// <para>
/// Version-mismatch behaviour on the reader:
/// no compatible compiled policy found → decision is DENY + high-priority alert.
/// </para>
/// </summary>
public static class AbacEngineVersion
{
    /// <summary>
    /// Current schema version string embedded in compiled policies and cache keys.
    /// Value is an integer string; increment by 1 on breaking changes.
    /// </summary>
    public const string Current = "1";

    /// <summary>
    /// Number of days old compiled sets are retained in Redis after a new set is promoted.
    /// Allows readers on older deployments to continue operating during rolling updates.
    /// </summary>
    public const int GracePeriodDays = 7;
}

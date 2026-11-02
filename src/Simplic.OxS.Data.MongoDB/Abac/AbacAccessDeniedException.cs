namespace Simplic.OxS.Data.MongoDB.Abac;

/// <summary>
/// Thrown by ABAC-enabled repositories when the read action is denied entirely for the
/// current subject (§7.5). Controllers / exception filters must map it to <b>403 Forbidden</b>.
/// A single resource that is merely filtered out by resource conditions is <b>not</b> reported
/// through this exception — the repository returns <c>null</c> (→ 404).
/// </summary>
public class AbacAccessDeniedException : Exception
{
    /// <summary>Initializes a new instance of <see cref="AbacAccessDeniedException"/>.</summary>
    /// <param name="action">The denied ABAC action.</param>
    public AbacAccessDeniedException(string action)
        : base($"ABAC: access denied for action '{action}'.")
    {
        Action = action;
    }

    /// <summary>The denied ABAC action.</summary>
    public string Action { get; }
}

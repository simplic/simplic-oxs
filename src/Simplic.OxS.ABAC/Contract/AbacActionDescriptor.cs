namespace Simplic.OxS.ABAC.Contract;

/// <summary>
/// Describes the set of subject and resource fields that are valid for a single action.
/// Registered in the action catalog of <see cref="IAbacCompileContract"/>.
/// </summary>
public class AbacActionDescriptor
{
    /// <summary>
    /// Fully-qualified action name or prefix pattern (e.g. <c>oxs:document:read</c>).
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Subject field paths that are allowed in conditions for this action
    /// (e.g. <c>["roles", "teams.name", "user.email"]</c>).
    /// Empty array means any subject field is permitted.
    /// </summary>
    public string[] AllowedSubjectFields { get; set; } = [];

    /// <summary>
    /// Resource field paths that are allowed in conditions for this action
    /// (e.g. <c>["department", "status", "tags"]</c>).
    /// Empty array means any resource field declared in field-mappings is permitted.
    /// </summary>
    public string[] AllowedResourceFields { get; set; } = [];
}

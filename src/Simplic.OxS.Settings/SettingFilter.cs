using Simplic.OxS.Data;

namespace Simplic.OxS.Settings;

/// <summary>
/// Filter for setting objects
/// </summary>
public class SettingFilter : OrganizationFilterBase
{
    /// <summary>
    /// Gets or sets the internal name of the setting.
    /// </summary>
    public string? InternalName { get; set; }
}
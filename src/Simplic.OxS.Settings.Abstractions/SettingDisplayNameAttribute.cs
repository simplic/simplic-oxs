using System;

namespace Simplic.OxS.Settings.Abstractions;

/// <summary>
/// Attribute to specify a custom display name for enum values in settings
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class SettingDisplayNameAttribute : Attribute
{
    /// <summary>
    /// Initialize display name attribute
    /// </summary>
    /// <param name="displayName">The human-readable display name for this enum value</param>
    public SettingDisplayNameAttribute(string displayName)
    {
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    /// <summary>
    /// The human-readable display name for this enum value
    /// </summary>
    public string DisplayName { get; }
}
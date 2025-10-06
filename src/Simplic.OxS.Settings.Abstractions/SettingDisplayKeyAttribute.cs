using System;

namespace Simplic.OxS.Settings.Abstractions;

/// <summary>
/// Attribute to specify a custom display key for enum values in settings
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class SettingDisplayKeyAttribute : Attribute
{
    /// <summary>
    /// Initialize display key attribute
    /// </summary>
    /// <param name="displayKey">The localization key for this enum value</param>
    public SettingDisplayKeyAttribute(string displayKey)
    {
        DisplayKey = displayKey ?? throw new ArgumentNullException(nameof(displayKey));
    }

    /// <summary>
    /// The localization key for this enum value
    /// </summary>
    public string DisplayKey { get; }
}
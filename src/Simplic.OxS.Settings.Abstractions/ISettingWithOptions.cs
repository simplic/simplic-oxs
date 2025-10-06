namespace Simplic.OxS.Settings.Abstractions;

/// <summary>
/// Represents an option in a setting that has predefined choices
/// </summary>
public class SettingOption
{
    /// <summary>
    /// Initialize setting option
    /// </summary>
    /// <param name="value">The actual value (enum value, string key, etc.)</param>
    /// <param name="displayName">Human-readable name</param>
    /// <param name="displayKey">Localization key for this option</param>
    public SettingOption(object value, string displayName, string? displayKey = null)
    {
        Value = value;
        DisplayName = displayName;
        DisplayKey = displayKey;
    }

    /// <summary>
    /// The actual value stored in the database
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Human-readable display name
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Localization key for this option
    /// </summary>
    public string? DisplayKey { get; }
}

/// <summary>
/// Interface for settings that have predefined options/choices
/// </summary>
public interface ISettingWithOptions : IOrganizationSettingDefinition
{
    /// <summary>
    /// Available options for this setting
    /// </summary>
    IReadOnlyList<SettingOption> Options { get; }
}
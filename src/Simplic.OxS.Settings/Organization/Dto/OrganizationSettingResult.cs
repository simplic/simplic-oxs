namespace Simplic.OxS.Settings.Organization.Dto;

/// <summary>
/// Result DTO for organization setting
/// </summary>
/// <param name="InternalName">Internal name of the setting</param>
/// <param name="DisplayName">Human-readable display name</param>
/// <param name="DisplayKey">Localization key</param>
/// <param name="Value">Current effective value</param>
/// <param name="DefaultValue">Default value from definition</param>
/// <param name="ValueType">Type name of the value</param>
public record OrganizationSettingResult(
    string InternalName,
    string DisplayName,
    string DisplayKey,
    object? Value,
    object? DefaultValue,
    string ValueType);

/// <summary>
/// Typed result DTO for organization setting
/// </summary>
/// <typeparam name="T">Value type</typeparam>
/// <param name="InternalName">Internal name of the setting</param>
/// <param name="DisplayName">Human-readable display name</param>
/// <param name="DisplayKey">Localization key</param>
/// <param name="Value">Current effective value</param>
/// <param name="DefaultValue">Default value from definition</param>
public record OrganizationSettingResult<T>(
    string InternalName,
    string DisplayName,
    string DisplayKey,
    T? Value,
    T? DefaultValue);
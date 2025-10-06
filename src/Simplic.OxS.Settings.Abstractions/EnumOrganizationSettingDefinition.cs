using System.ComponentModel;
using System.Reflection;

namespace Simplic.OxS.Settings.Abstractions;

/// <summary>
/// Base class for enum-based organization settings with rich display information
/// </summary>
/// <typeparam name="TEnum">The enum type</typeparam>
public abstract class EnumOrganizationSettingDefinition<TEnum> : OrganizationSettingDefinition<TEnum>, ISettingWithOptions
    where TEnum : struct, Enum
{
    private readonly IReadOnlyList<SettingOption> _options;

    /// <summary>
    /// Initialize enum setting definition
    /// </summary>
    /// <param name="internalName">Internal name for storage and API</param>
    /// <param name="displayKey">Display key for localization</param>
    /// <param name="displayName">Human-readable name</param>
    /// <param name="defaultValue">Default enum value</param>
    protected EnumOrganizationSettingDefinition(
        string internalName,
        string displayKey,
        string displayName,
        TEnum defaultValue) 
        : base(internalName, displayKey, displayName, defaultValue)
    {
        _options = BuildOptionsFromEnum(displayKey);
    }

    /// <summary>
    /// Initialize enum setting definition with custom options
    /// </summary>
    /// <param name="internalName">Internal name for storage and API</param>
    /// <param name="displayKey">Display key for localization</param>
    /// <param name="displayName">Human-readable name</param>
    /// <param name="defaultValue">Default enum value</param>
    /// <param name="customOptions">Custom options with display names and keys</param>
    protected EnumOrganizationSettingDefinition(
        string internalName,
        string displayKey,
        string displayName,
        TEnum defaultValue,
        IReadOnlyList<SettingOption> customOptions)
        : base(internalName, displayKey, displayName, defaultValue)
    {
        _options = customOptions;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SettingOption> Options => _options;

    /// <summary>
    /// Build options from enum using reflection and attributes
    /// </summary>
    private static IReadOnlyList<SettingOption> BuildOptionsFromEnum(string baseDisplayKey)
    {
        var enumType = typeof(TEnum);
        var options = new List<SettingOption>();

        foreach (TEnum enumValue in Enum.GetValues<TEnum>())
        {
            var fieldInfo = enumType.GetField(enumValue.ToString());
            var displayName = GetDisplayName(fieldInfo, enumValue.ToString());
            var optionDisplayKey = $"{baseDisplayKey}.{enumValue.ToString().ToLowerInvariant()}";


            options.Add(new SettingOption(enumValue, displayName, optionDisplayKey));
        }

        return options.AsReadOnly();
    }

    /// <summary>
    /// Get display name from DisplayName attribute or enum name
    /// </summary>
    private static string GetDisplayName(FieldInfo? fieldInfo, string fallback)
    {
        var displayNameAttr = fieldInfo?.GetCustomAttribute<DisplayNameAttribute>();
        return displayNameAttr?.DisplayName ?? fallback.Replace("_", " ");
    }
}
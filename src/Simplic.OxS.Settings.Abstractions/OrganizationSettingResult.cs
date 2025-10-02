namespace Simplic.OxS.Settings.Abstractions;

/// <summary>
/// Result of retrieving an organization setting
/// </summary>
public class OrganizationSettingResult
{
    /// <summary>
    /// Initialize setting result
    /// </summary>
    public OrganizationSettingResult(
        string internalName,
        string displayName,
        string displayKey,
        object? value,
        object? defaultValue,
        string valueTypeName,
        IReadOnlyList<SettingOption>? options = null)
    {
        InternalName = internalName;
        DisplayName = displayName;
        DisplayKey = displayKey;
        Value = value;
        DefaultValue = defaultValue;
        ValueTypeName = valueTypeName;
        Options = options;
    }

    /// <summary>
    /// Internal name for storage and API access
    /// </summary>
    public string InternalName { get; }
    
    /// <summary>
    /// Human-readable display name
    /// </summary>
    public string DisplayName { get; }
    
    /// <summary>
    /// Display key for localization
    /// </summary>
    public string DisplayKey { get; }
    
    /// <summary>
    /// Current effective value (organization override or default)
    /// </summary>
    public object? Value { get; }
    
    /// <summary>
    /// Default value from definition
    /// </summary>
    public object? DefaultValue { get; }
    
    /// <summary>
    /// Name of the value type
    /// </summary>
    public string ValueTypeName { get; }

    /// <summary>
    /// Available options for this setting (null if no predefined options)
    /// </summary>
    public IReadOnlyList<SettingOption>? Options { get; }

    /// <summary>
    /// Whether this setting has predefined options
    /// </summary>
    public bool HasOptions => Options != null && Options.Count > 0;
}

/// <summary>
/// Typed result of retrieving an organization setting
/// </summary>
/// <typeparam name="T">Type of the setting value</typeparam>
public class OrganizationSettingResult<T>
{
    /// <summary>
    /// Initialize typed setting result
    /// </summary>
    public OrganizationSettingResult(
        string internalName,
        string displayName,
        string displayKey,
        T? value,
        T? defaultValue,
        IReadOnlyList<SettingOption>? options = null)
    {
        InternalName = internalName;
        DisplayName = displayName;
        DisplayKey = displayKey;
        Value = value;
        DefaultValue = defaultValue;
        Options = options;
    }

    /// <summary>
    /// Internal name for storage and API access
    /// </summary>
    public string InternalName { get; }
    
    /// <summary>
    /// Human-readable display name
    /// </summary>
    public string DisplayName { get; }
    
    /// <summary>
    /// Display key for localization
    /// </summary>
    public string DisplayKey { get; }
    
    /// <summary>
    /// Current effective value (organization override or default)
    /// </summary>
    public T? Value { get; }
    
    /// <summary>
    /// Default value from definition
    /// </summary>
    public T? DefaultValue { get; }

    /// <summary>
    /// Available options for this setting (null if no predefined options)
    /// </summary>
    public IReadOnlyList<SettingOption>? Options { get; }

    /// <summary>
    /// Whether this setting has predefined options
    /// </summary>
    public bool HasOptions => Options != null && Options.Count > 0;
}
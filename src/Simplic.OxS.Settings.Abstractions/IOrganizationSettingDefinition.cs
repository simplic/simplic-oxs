namespace Simplic.OxS.Settings.Abstractions;

/// <summary>
/// Represents a setting definition without type information
/// </summary>
public interface IOrganizationSettingDefinition
{
    /// <summary>
    /// Internal name used for storage and API access
    /// </summary>
    string InternalName { get; }
    
    /// <summary>
    /// Display key for localization
    /// </summary>
    string DisplayKey { get; }
    
    /// <summary>
    /// Human-readable display name
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// The .NET type of the setting value
    /// </summary>
    Type ValueType { get; }
    
    /// <summary>
    /// Default value if no organization override exists
    /// </summary>
    object? DefaultValue { get; }
    
    /// <summary>
    /// Group key for organizing related settings
    /// </summary>
    string? GroupKey { get; }
    
    /// <summary>
    /// Display key for group localization
    /// </summary>
    string? GroupDisplayKey { get; }
    
    /// <summary>
    /// Human-readable group display name
    /// </summary>
    string? GroupDisplayName { get; }
}
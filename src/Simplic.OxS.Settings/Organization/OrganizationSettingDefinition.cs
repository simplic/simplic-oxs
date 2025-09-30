namespace Simplic.OxS.Settings.Organization;

/// <summary>
/// Base class for typed organization setting definitions
/// </summary>
/// <typeparam name="T">The type of the setting value</typeparam>
public abstract class OrganizationSettingDefinition<T> : IOrganizationSettingDefinition
{
    /// <summary>
    /// Initialize setting definition
    /// </summary>
    /// <param name="internalName">Internal name for storage and API</param>
    /// <param name="displayKey">Display key for localization</param>
    /// <param name="displayName">Human-readable name</param>
    /// <param name="defaultValue">Default value</param>
    protected OrganizationSettingDefinition(
        string internalName,
        string displayKey, 
        string displayName,
        T? defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(internalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        
        if (!IsSupportedType(typeof(T)))
            throw new NotSupportedException($"Type {typeof(T).Name} is not a supported setting value type.");
            
        InternalName = internalName;
        DisplayKey = displayKey;
        DisplayName = displayName;
        DefaultValue = defaultValue;
    }

    /// <inheritdoc/>
    public string InternalName { get; }
    
    /// <inheritdoc/>
    public string DisplayKey { get; }
    
    /// <inheritdoc/>
    public string DisplayName { get; }
    
    /// <inheritdoc/>
    public Type ValueType => typeof(T);
    
    /// <inheritdoc/>
    public object? DefaultValue { get; }

    /// <summary>
    /// Check if the type is supported for settings
    /// </summary>
    /// <param name="type">Type to check</param>
    /// <returns>True if supported</returns>
    private static bool IsSupportedType(Type type)
    {
        // Handle nullable types
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        
        if (actualType.IsEnum) 
            return true;
            
        return actualType == typeof(string) ||
               actualType == typeof(bool) ||
               actualType == typeof(int) ||
               actualType == typeof(long) ||
               actualType == typeof(decimal) ||
               actualType == typeof(double) ||
               actualType == typeof(float) ||
               actualType == typeof(Guid) ||
               actualType == typeof(DateTime) ||
               actualType == typeof(DateOnly) ||
               actualType == typeof(TimeOnly);
    }
}
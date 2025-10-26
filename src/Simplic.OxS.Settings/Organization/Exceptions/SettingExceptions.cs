namespace Simplic.OxS.Settings.Organization.Exceptions;

/// <summary>
/// Exception thrown when a setting is not found
/// </summary>
public class SettingNotFoundException : Exception
{
    /// <summary>
    /// Initialize exception
    /// </summary>
    /// <param name="internalName">Internal name of the setting</param>
    public SettingNotFoundException(string internalName) 
        : base($"Setting '{internalName}' was not found.")
    {
        InternalName = internalName;
    }

    /// <summary>
    /// Internal name of the setting that was not found
    /// </summary>
    public string InternalName { get; }
}

/// <summary>
/// Exception thrown when setting value type doesn't match expected type
/// </summary>
public class SettingTypeMismatchException : Exception
{
    /// <summary>
    /// Initialize exception
    /// </summary>
    /// <param name="internalName">Internal name of the setting</param>
    /// <param name="expectedType">Expected type</param>
    /// <param name="actualType">Actual type provided</param>
    public SettingTypeMismatchException(string internalName, Type expectedType, Type actualType)
        : base($"Setting '{internalName}' expects type '{expectedType.Name}' but got '{actualType.Name}'.")
    {
        InternalName = internalName;
        ExpectedType = expectedType;
        ActualType = actualType;
    }

    /// <summary>
    /// Internal name of the setting
    /// </summary>
    public string InternalName { get; }
    
    /// <summary>
    /// Expected type
    /// </summary>
    public Type ExpectedType { get; }
    
    /// <summary>
    /// Actual type provided
    /// </summary>
    public Type ActualType { get; }
}

/// <summary>
/// Exception thrown when setting value is null but not allowed
/// </summary>
public class SettingValueNullException : Exception
{
    /// <summary>
    /// Initialize exception
    /// </summary>
    /// <param name="internalName">Internal name of the setting</param>
    public SettingValueNullException(string internalName)
        : base($"Setting '{internalName}' value cannot be null.")
    {
        InternalName = internalName;
    }

    /// <summary>
    /// Internal name of the setting
    /// </summary>
    public string InternalName { get; }
}

/// <summary>
/// Exception thrown when persistence layer is unavailable
/// </summary>
public class PersistenceUnavailableException : Exception
{
    /// <summary>
    /// Initialize exception
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Inner exception</param>
    public PersistenceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
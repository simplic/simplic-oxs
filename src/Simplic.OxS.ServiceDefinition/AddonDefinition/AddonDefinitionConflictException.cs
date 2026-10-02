namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// A write of an <see cref="AddonDefinitionDocument"/> was refused because the organisation
/// already holds a row of the same entity id and path: the store keeps one row per
/// organisation, entity id and path, whatever two requests read before they wrote.
/// </summary>
public class AddonDefinitionConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="AddonDefinitionConflictException"/>.
    /// </summary>
    /// <param name="message">What was refused.</param>
    /// <param name="innerException">The store's own error, if any.</param>
    public AddonDefinitionConflictException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}

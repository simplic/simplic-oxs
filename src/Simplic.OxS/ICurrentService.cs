namespace Simplic.OxS;

/// <summary>
/// Represents a service that provides information about its identity and version.
/// </summary>
/// <remarks>Implementations of this interface can be used to access metadata about the current service instance,
/// such as its name and version. This is useful for logging, diagnostics, or service discovery scenarios.</remarks>
public interface ICurrentService
{
    /// <summary>
    /// Gets or sets the name of the service associated with this instance.
    /// </summary>
    public string ServiceName { get; set; }

    /// <summary>
    /// Gets or sets the version identifier for the current instance.
    /// </summary>
    public string ApiVersion { get; set; }
}

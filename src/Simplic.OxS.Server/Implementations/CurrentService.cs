namespace Simplic.OxS.Server.Implementations;

/// <summary>
/// Provides access to the current service's metadata, including its name and version.
/// </summary>
internal class CurrentService : ICurrentService
{
    /// <inheritdoc />
    public string ServiceName { get; set; }

    /// <inheritdoc />
    public string ApiVersion { get; set; }
}

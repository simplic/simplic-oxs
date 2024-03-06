namespace Simplic.OxS.InternalClient;

public class StreamResult
{
    /// <summary>
    /// Gets or sets the stream.
    /// </summary>
    public Stream Stream { get; set; }

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the mime type.
    /// </summary>
    public string? MimeType { get; set; }
}
namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>415 Unsupported Media Type</c> response.
/// Throw when the request's <c>Content-Type</c> is not supported by the target endpoint.
/// </summary>
public class UnsupportedMediaTypeException : OxSException
{
    /// <summary>
    /// Initializes a new <see cref="UnsupportedMediaTypeException"/>.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public UnsupportedMediaTypeException(string? message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public override int StatusCode => 415;

    /// <inheritdoc/>
    public override string? Title => "Unsupported Media Type";

    /// <inheritdoc/>
    public override string? ProblemType => "urn:simplic-oxs:problem:unsupported-media-type";
}

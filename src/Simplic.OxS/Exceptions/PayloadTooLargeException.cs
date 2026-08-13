namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>413 Content Too Large</c> response.
/// Throw when the request payload exceeds an allowed size limit.
/// </summary>
public class PayloadTooLargeException : OxSException
{
    /// <summary>
    /// Initializes a new <see cref="PayloadTooLargeException"/>.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public PayloadTooLargeException(string? message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public override int StatusCode => 413;

    /// <inheritdoc/>
    public override string? Title => "Content Too Large";

    /// <inheritdoc/>
    public override string? ProblemType => "urn:simplic-oxs:problem:payload-too-large";
}

namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>409 Conflict</c> response.
/// Throw when a request conflicts with the current state of the resource
/// (e.g. a concurrent update or a business-state conflict).
/// </summary>
public class ConflictException : OxSException
{
    /// <summary>
    /// Initializes a new <see cref="ConflictException"/>.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public ConflictException(string? message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public override int StatusCode => 409;

    /// <inheritdoc/>
    public override string? Title => "Conflict";

    /// <inheritdoc/>
    public override string? ProblemType => "urn:simplic-oxs:problem:conflict";
}

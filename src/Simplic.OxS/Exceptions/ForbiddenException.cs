namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>403 Forbidden</c> response.
/// Throw when the caller is authenticated but not permitted to access the resource.
/// </summary>
public class ForbiddenException : OxSException
{
    /// <summary>
    /// Initializes a new <see cref="ForbiddenException"/>.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public ForbiddenException(string? message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public override int StatusCode => 403;

    /// <inheritdoc/>
    public override string? Title => "Forbidden";
}

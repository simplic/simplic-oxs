namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>401 Unauthorized</c> response.
/// Throw when the request lacks valid authentication credentials.
/// </summary>
public class UnauthorizedException : OxSException
{
    /// <summary>
    /// Initializes a new <see cref="UnauthorizedException"/>.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public UnauthorizedException(string? message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public override int StatusCode => 401;

    /// <inheritdoc/>
    public override string? Title => "Unauthorized";

    /// <inheritdoc/>
    public override string? ProblemType => "urn:simplic-oxs:problem:unauthorized";

    /// <summary>
    /// Emits the <c>WWW-Authenticate</c> header required by RFC 9110 on a 401 response.
    /// Defaults to the platform's <c>Bearer</c> (JWT) scheme.
    /// </summary>
    /// <inheritdoc/>
    public override void PopulateHeaders(IDictionary<string, string> headers)
    {
        headers["WWW-Authenticate"] = "Bearer";
    }
}

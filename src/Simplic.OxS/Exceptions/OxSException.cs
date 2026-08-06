namespace Simplic.OxS.Exceptions;

/// <summary>
/// Base class for all exceptions that carry their own HTTP response metadata.
/// <para>
/// A dedicated exception filter in the server layer reads this metadata and builds the
/// matching HTTP response (RFC 9457 <c>ProblemDetails</c> or a plain message body). This
/// keeps the exception type free of any ASP.NET / MVC dependency, so it can be thrown from
/// any layer (domain, service, controller).
/// </para>
/// </summary>
public abstract class OxSException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="OxSException"/>.
    /// </summary>
    /// <param name="message">The exception message. Surfaced as <c>ProblemDetails.Detail</c>.</param>
    /// <param name="innerException">Optional inner exception.</param>
    protected OxSException(string? message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// The HTTP status code the response should use.
    /// </summary>
    public abstract int StatusCode { get; }

    /// <summary>
    /// When <see langword="true"/> (default) the response is an RFC 9457 <c>ProblemDetails</c> body.
    /// When <see langword="false"/> the plain <see cref="Exception.Message"/> is written as the body.
    /// </summary>
    public virtual bool IncludeProblemDetails => true;

    /// <summary>
    /// The <c>ProblemDetails.Title</c>. When <see langword="null"/> the filter falls back to the
    /// reason phrase of <see cref="StatusCode"/>.
    /// </summary>
    public virtual string? Title => null;

    /// <summary>
    /// The <c>ProblemDetails.Type</c> URI. When <see langword="null"/> the filter falls back to
    /// <c>about:blank</c>.
    /// </summary>
    public virtual string? ProblemType => null;

    /// <summary>
    /// Adds exception-specific members to the <c>ProblemDetails.Extensions</c> dictionary.
    /// Override to enrich the problem details with structured, machine-readable fields.
    /// </summary>
    /// <param name="extensions">The extensions dictionary to populate.</param>
    public virtual void PopulateProblemDetails(IDictionary<string, object?> extensions)
    {
    }
}

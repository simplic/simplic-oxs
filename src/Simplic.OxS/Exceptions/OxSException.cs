namespace Simplic.OxS.Exceptions;

/// <summary>
/// Base class for all exceptions that carry their own HTTP response metadata.
/// <para>
/// A dedicated exception handler in the server layer reads this metadata and builds the
/// matching HTTP response — always an RFC 9457 <c>application/problem+json</c> body. This
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
    /// The client-facing <c>ProblemDetails.Detail</c>. Defaults to <see cref="Exception.Message"/>.
    /// <para>
    /// Override when the <see cref="Exception.Message"/> is meant for logs only and a separate,
    /// client-safe description should be surfaced in the response body.
    /// </para>
    /// </summary>
    public virtual string? Detail => Message;

    /// <summary>
    /// The <c>ProblemDetails.Title</c>. When <see langword="null"/> the handler falls back to the
    /// reason phrase of <see cref="StatusCode"/>.
    /// </summary>
    public virtual string? Title => null;

    /// <summary>
    /// The <c>ProblemDetails.Type</c> URI. When <see langword="null"/> the handler falls back to
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

    /// <summary>
    /// Adds exception-specific HTTP response headers. Override to emit headers that HTTP clients
    /// honour directly rather than reading from the body — e.g. <c>Retry-After</c> on a 429/503,
    /// or <c>WWW-Authenticate</c> on a 401 (required by RFC 9110).
    /// <para>
    /// The dictionary uses only BCL types so the core exception stays free of any ASP.NET
    /// dependency; the server-layer handler copies the entries onto the response.
    /// </para>
    /// </summary>
    /// <param name="headers">The header dictionary to populate (header name to value).</param>
    public virtual void PopulateHeaders(IDictionary<string, string> headers)
    {
    }
}

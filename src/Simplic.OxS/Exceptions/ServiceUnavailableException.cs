namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>503 Service Unavailable</c> response.
/// Throw when the service (or a dependency it needs) is temporarily unable to handle the request.
/// <para>
/// When a <c>retryAfter</c> hint is supplied it is emitted as the <c>Retry-After</c> response header
/// (delta-seconds), which HTTP clients honour directly.
/// </para>
/// </summary>
public class ServiceUnavailableException : OxSException
{
    private readonly TimeSpan? retryAfter;

    /// <summary>
    /// Initializes a new <see cref="ServiceUnavailableException"/>.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="retryAfter">Optional hint for how long the client should wait before retrying.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public ServiceUnavailableException(string? message, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(message, innerException)
    {
        this.retryAfter = retryAfter;
    }

    /// <inheritdoc/>
    public override int StatusCode => 503;

    /// <inheritdoc/>
    public override string? Title => "Service Unavailable";

    /// <inheritdoc/>
    public override string? ProblemType => "urn:simplic-oxs:problem:service-unavailable";

    /// <inheritdoc/>
    public override void PopulateHeaders(IDictionary<string, string> headers)
    {
        if (retryAfter is { } value)
            headers["Retry-After"] = RetryAfter.ToDeltaSeconds(value);
    }
}

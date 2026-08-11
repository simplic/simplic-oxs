namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception that maps to an HTTP <c>429 Too Many Requests</c> response.
/// Throw when a client exceeds a rate limit (e.g. a per-organization throttle).
/// <para>
/// When a <c>retryAfter</c> hint is supplied it is emitted as the <c>Retry-After</c> response header
/// (delta-seconds), which HTTP clients honour directly.
/// </para>
/// </summary>
public class TooManyRequestsException : OxSException
{
    private readonly TimeSpan? retryAfter;

    /// <summary>
    /// Initializes a new <see cref="TooManyRequestsException"/>.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="retryAfter">Optional hint for how long the client should wait before retrying.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public TooManyRequestsException(string? message, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(message, innerException)
    {
        this.retryAfter = retryAfter;
    }

    /// <inheritdoc/>
    public override int StatusCode => 429;

    /// <inheritdoc/>
    public override string? Title => "Too Many Requests";

    /// <inheritdoc/>
    public override string? ProblemType => "urn:simplic-oxs:problem:too-many-requests";

    /// <inheritdoc/>
    public override void PopulateHeaders(IDictionary<string, string> headers)
    {
        if (retryAfter is { } value)
            headers["Retry-After"] = RetryAfter.ToDeltaSeconds(value);
    }
}

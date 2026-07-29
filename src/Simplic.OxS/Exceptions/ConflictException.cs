namespace Simplic.OxS;

/// <summary>
/// Exception thrown when a request conflicts with the current state of the resource —
/// a stale ETag, a concurrent update, or a business-state conflict.
/// <para>
/// Produces <c>409 Conflict</c> with error code <c>conflict</c> via the global exception
/// handler.
/// </para>
/// <example>
/// <code>
/// throw new ConflictException("Shipment was modified by another user.");
/// </code>
/// </example>
/// </summary>
public class ConflictException : Exception, IOxSException
{
    /// <summary>
    /// Initializes a new conflict exception.
    /// </summary>
    /// <param name="message">
    /// Message describing the conflict. Returned to the caller — keep it safe to expose.
    /// </param>
    public ConflictException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new conflict exception.
    /// </summary>
    /// <param name="message">
    /// Message describing the conflict. Returned to the caller — keep it safe to expose.
    /// </param>
    /// <param name="innerException">The underlying cause.</param>
    public ConflictException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public virtual int StatusCode => 409;

    /// <inheritdoc/>
    public virtual string ErrorCode => "conflict";

    /// <inheritdoc/>
    public virtual IReadOnlyDictionary<string, object?> ProblemExtensions
        => new Dictionary<string, object?>();
}

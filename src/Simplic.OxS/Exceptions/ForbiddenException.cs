namespace Simplic.OxS;

/// <summary>
/// Exception thrown when the caller is authenticated but not permitted to act on the
/// resource — e.g. the resource belongs to a different organization, or the user lacks
/// the required role.
/// <para>
/// Produces <c>403 Forbidden</c> with error code <c>forbidden</c> via the global exception
/// handler. Use this rather than <c>404</c> when you have already established the caller
/// is authenticated; use <see cref="ResourceNotFoundException"/> when the resource genuinely
/// does not exist.
/// </para>
/// <para>
/// Do not include the resource owner's identity in the message — that leaks cross-tenant
/// information to the caller.
/// </para>
/// </summary>
public class ForbiddenException : Exception, IOxSException
{
    /// <summary>
    /// Initializes a new forbidden exception.
    /// </summary>
    /// <param name="message">
    /// Message describing what was denied. Returned to the caller — keep it safe to expose
    /// and free of cross-tenant detail.
    /// </param>
    public ForbiddenException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new forbidden exception.
    /// </summary>
    /// <param name="message">
    /// Message describing what was denied. Returned to the caller — keep it safe to expose
    /// and free of cross-tenant detail.
    /// </param>
    /// <param name="innerException">The underlying cause.</param>
    public ForbiddenException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public virtual int StatusCode => 403;

    /// <inheritdoc/>
    public virtual string ErrorCode => "forbidden";

    /// <inheritdoc/>
    public virtual IReadOnlyDictionary<string, object?> ProblemExtensions
        => new Dictionary<string, object?>();
}

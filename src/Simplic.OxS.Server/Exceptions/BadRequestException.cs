namespace Simplic.OxS.Server
{
    /// <summary>
    /// Exception to throw when the request is bad, or not as it should be.
    /// <para>
    /// Produces <c>400 Bad Request</c> with error code <c>bad_request</c> via the global
    /// exception handler. You do <b>not</b> need to catch this in the controller, and you do
    /// not need <see cref="Exceptions.BadRequestExceptionFilterAttribute"/> — the handler is
    /// registered globally by <see cref="Bootstrap"/>.
    /// </para>
    /// <para>
    /// The message is returned to the caller, so it must be safe to expose: describe the
    /// business rule that was violated, never internal state or secrets.
    /// </para>
    /// </summary>
    public class BadRequestException : Exception, IOxSException
    {
        /// <summary>
        /// Initializes a new bad request exception.
        /// </summary>
        /// <param name="message">The exception message. Returned to the caller — keep it safe to expose.</param>
        public BadRequestException(string? message) : base(message)
        {
        }

        /// <inheritdoc/>
        public virtual int StatusCode => 400;

        /// <inheritdoc/>
        public virtual string ErrorCode => "bad_request";

        /// <inheritdoc/>
        public virtual IReadOnlyDictionary<string, object?> ProblemExtensions
            => new Dictionary<string, object?>();
    }
}

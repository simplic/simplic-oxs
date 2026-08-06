namespace Simplic.OxS.Server
{
    /// <summary>
    /// Exception to throw when the request is bad, or not as it should be.
    /// <para>
    /// Retained for backward compatibility. New code should prefer
    /// <see cref="Simplic.OxS.Exceptions.BadRequestException"/> (and the other
    /// <see cref="Simplic.OxS.Exceptions.OxSException"/> derivatives) directly. This type derives
    /// from it, so it is still handled by the unified exception filter and produces the same
    /// <c>400 Bad Request</c> RFC 9457 problem details response.
    /// </para>
    /// </summary>
    public class BadRequestException : Simplic.OxS.Exceptions.BadRequestException
    {
        /// <summary>
        /// Initializes a new bad request exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public BadRequestException(string? message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new bad request exception for a single failed field.
        /// </summary>
        /// <param name="propertyPath">The path of the property that failed (e.g. <c>customer.name</c>).</param>
        /// <param name="problem">A human-readable description of why the field is invalid.</param>
        public BadRequestException(string propertyPath, string problem) : base(propertyPath, problem)
        {
        }

        /// <summary>
        /// Initializes a new bad request exception from a set of field errors.
        /// </summary>
        /// <param name="errors">A map of property path to one or more problem messages.</param>
        /// <param name="message">Optional detail message.</param>
        public BadRequestException(
            System.Collections.Generic.IReadOnlyDictionary<string, string[]> errors,
            string? message = null) : base(errors, message)
        {
        }
    }
}

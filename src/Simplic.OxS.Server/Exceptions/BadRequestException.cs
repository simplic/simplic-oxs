namespace Simplic.OxS.Server
{
    /// <summary>
    /// Exception to throw when the request is bad, or not as it should be.
    /// <para>
    /// Should be catched in the controllers to return a bad request instead of an internal server error.
    /// </para>
    /// </summary>
    public class BadRequestException : Exception
    {
        /// <summary>
        /// Initializes a new bad request exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public BadRequestException(string? message) : base(message)
        {
        }
    }
}

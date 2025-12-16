namespace Simplic.OxS.Server.Services
{
    /// <summary>
    /// Represents the actual request context and contains all data, that needs to be passed
    /// through.
    /// </summary>
    public class RequestContext : IRequestContext
    {
        /// <summary>
        /// Gets or sets the actual user id, that belongs to the current request.
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Gets or sets the actual organization id, that belongs to the current request.
        /// </summary>
        public Guid? OrganizationId { get; set; }

        /// <summary>
        /// Gets or sets the current context/correlation id.
        /// </summary>
        public Guid? CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets a list of custom oxs-headers, passed through the request.
        /// E.g. oxs-skip-flow: true
        /// </summary>
        public IDictionary<string, string> OxSHeaders { get; set; } = new Dictionary<string, string>();
    }
}
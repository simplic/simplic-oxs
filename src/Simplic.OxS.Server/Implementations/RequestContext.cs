namespace Simplic.OxS.Server.Services
{
    /// <summary>
    /// Represents the actual request context and contains all data, that needs to be passed
    /// through
    /// </summary>
    public class RequestContext : IRequestContext
    {
        /// <summary>
        /// Gets or sets the actual user id, that belongs to the current request
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Gets or sets the actual tenant id, that belongs to the current request
        /// </summary>
        public Guid? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the current context/correlation id
        /// </summary>
        public Guid? CorrelationId { get; set; }
    }
}

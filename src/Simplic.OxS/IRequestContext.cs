namespace Simplic.OxS
{
    /// <summary>
    /// Represents the context of the actual request
    /// </summary>
    public interface IRequestContext
    {
        /// <summary>
        /// Gets or sets the current user id
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// Gets or sets the current organization id
        /// </summary>
        public Guid? OrganizationId { get; set; }

        /// <summary>
        /// Gets or sets the current correlation id
        /// </summary>
        public Guid? CorrelationId { get; set; }
    }
}

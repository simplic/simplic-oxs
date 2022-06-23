namespace Simplic.OxS.Data.MongoDB
{
    /// <summary>
    /// Organization data filter
    /// </summary>
    public class OrganizationFilterBase : IOrganizationFilter<Guid>
    {
        /// <summary>
        /// Gets or sets the data id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the organization id
        /// </summary>
        public Guid? OrganizationId { get; set; }

        /// <summary>
        /// Gets or sets whether to filter deleted data
        /// </summary>
        public bool? IsDeleted { get; set; } = false;
        public bool QueryAllOrganizations { get; set; } = false;
        public IList<Guid> IncludeIds { get; set; }
        public Guid? ExcludeId { get; set; }
    }
}

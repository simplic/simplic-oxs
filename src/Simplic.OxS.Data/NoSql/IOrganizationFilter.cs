namespace Simplic.OxS.Data
{
    /// <summary>
    /// Represents a basic no sql data filter
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IOrganizationFilter<TId> : IFilter<TId>
    {
        /// <summary>
        /// Gets or sets the organization id
        /// </summary>
        Guid? OrganizationId { get; set; }
    }
}
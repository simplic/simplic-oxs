namespace Simplic.OxS.Data
{
    /// <summary>
    /// Basic no sql organization based document
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IOrganizationDocument<TId> : IDocument<TId>
    {
        /// <summary>
        /// Gets or sets the organization id
        /// </summary>
        Guid OrganizationId { get; set; }
    }
}
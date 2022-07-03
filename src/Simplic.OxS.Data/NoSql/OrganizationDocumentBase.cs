using MongoDB.Bson.Serialization.Attributes;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Base document for storing organizsation specific data
    /// </summary>
    public abstract class OrganizationDocumentBase : IOrganizationDocument<Guid>
    {
        /// <summary>
        /// Gets or sets the id
        /// </summary>
        [BsonId]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the organizsation id
        /// </summary>
        public Guid OrganizationId { get; set; }

        /// <summary>
        /// Gets or sets whether the object is deleted
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}

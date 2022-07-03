namespace Simplic.OxS.Data
{
    /// <summary>
    /// Basic no sql document
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IDocument<TId> : IDefaultDocument
    {
        /// <summary>
        /// Gets or sets the document id
        /// </summary>
        TId Id { get; set; }

        /// <summary>
        /// Gets or sets whether the document is deleted
        /// </summary>
        bool IsDeleted { get; set; }
    }
}
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Data.Service
{
    /// <summary>
    /// Interface for services that manage <see cref="IOrganizationDocument{TId}"/>s.
    /// </summary>
    /// <typeparam name="TDocument">Document type (must inherit from <see cref="IOrganizationDocument{TId}"/>)</typeparam>
    /// <typeparam name="TFilter">Filter type (must inherit from <see cref="IOrganizationFilter{TId}"/>)</typeparam>
    public interface IOrganizationServiceBase<TDocument, TFilter>
        where TDocument : IOrganizationDocument<Guid>
        where TFilter : IOrganizationFilter<Guid>
    {
        /// <summary>
        /// Gets a document by it's id.
        /// </summary>
        /// <param name="id">Id of the document</param>
        /// <returns></returns>
        Task<TDocument?> GetById(Guid id);

        /// <summary>
        /// Creates an entry for given document.
        /// </summary>
        /// <param name="obj">Document of which an entry is to be created</param>
        /// <returns></returns>
        Task<TDocument> Create([NotNull] TDocument obj);

        /// <summary>
        /// Updates an entry for given document.
        /// </summary>
        /// <param name="obj">Updated document</param>
        /// <returns></returns>
        Task<TDocument> Update([NotNull] TDocument obj);

        /// <summary>
        /// Deletes entry for given document.
        /// </summary>
        /// <param name="obj">Document to be deleted</param>
        /// <returns></returns>
        Task<TDocument> Delete([NotNull] TDocument obj);

        /// <summary>
        /// Deletes entry where document matches given id.
        /// </summary>
        /// <param name="id">Id of the document that is to be deleted</param>
        /// <returns></returns>
        Task Delete(Guid id);
    }
}
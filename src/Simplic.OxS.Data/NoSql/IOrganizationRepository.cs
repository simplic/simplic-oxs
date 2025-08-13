using System.Linq.Expressions;
using HotChocolate;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Basic repository
    /// </summary>
    /// <typeparam name="TId">PK (ID) type</typeparam>
    /// <typeparam name="TDocument">Entity type</typeparam>
    /// <typeparam name="TFilter">Filter type</typeparam>
    public interface IOrganizationRepository<TId, TDocument, TFilter> : IRepository<TId, TDocument, TFilter>
        where TDocument : IOrganizationDocument<TId>
        where TFilter : IOrganizationFilter<TId>
    {
        /// <summary>
        /// Get an entity by its id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryAllOrganizations"></param>
        /// <returns>Entity</returns>
        Task<TDocument> GetAsync(TId id, bool queryAllOrganizations = false);

        /// <summary>
        /// Gets the collection of an document as an queryable.
        /// </summary>
        /// <returns></returns>
		Task<IExecutable<TDocument>> GetCollection();

        /// <summary>
        /// Updates a property for documents matching the filter.
        /// </summary>
        /// <param name="filter">Filter for documents</param>
        /// <param name="selector">Selects the property that shall be updated</param>
        /// <param name="newValue">New value to set for that property</param>
        /// <typeparam name="TProperty">Type of the property</typeparam>
        Task UpdatePropertyByFilter<TProperty>(
            TFilter filter,
            Expression<Func<TDocument, TProperty>> selector,
            TProperty newValue
        );

        /// <summary>
        /// Updates a property for a specific document with given ID.
        /// </summary>
        /// <param name="id">ID of the document</param>
        /// <param name="selector">Selects the property that shall be updated</param>
        /// <param name="newValue">New value to set for that property</param>
        /// <typeparam name="TProperty">Type of the property</typeparam>
        Task UpdateProperty<TProperty>(
            Guid id,
            Expression<Func<TDocument, TProperty>> selector,
            TProperty newValue
        );
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Basic repository
    /// </summary>
    /// <typeparam name="TId">PK (ID) type</typeparam>
    /// <typeparam name="TDocument">Entity type</typeparam>
    /// <typeparam name="TFilter">Filter type</typeparam>
    public interface IOrganizationRepository<TId, TDocument, TFilter> : IRepository<TId, TDocument, TFilter>
        where TDocument : IDocument<TId>
        where TFilter : IFilter<TId>
    {
        /// <summary>
        /// Get an entity by its id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryAllOrganizations"></param>
        /// <returns>Entity</returns>
        Task<TDocument> GetAsync(TId id, bool queryAllOrganizations = false);

        /// <summary>
        /// Get all entities from data source
        /// </summary>
        /// <param name="queryAllOrganizations"></param>
        /// <returns>Enumerable of entities</returns>
        Task<IEnumerable<TDocument>> GetAllAsync(bool queryAllOrganizations = false);
    }
}

using System;
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
    public interface IRepository<TId, TDocument, TFilter> : IReadOnlyRepository<TId, TDocument, TFilter>
        where TDocument : IDocument<TId>
        where TFilter : IFilter<TId>
    {
        /// <summary>
        /// Create new entity
        /// </summary>
        /// <param name="entity">Entity to create</param>
        Task CreateAsync(TDocument entity);

        /// <summary>
        /// Update an entity in the database
        /// </summary>
        /// <param name="obj"></param>
        Task UpdateAsync(TDocument obj);

        /// <summary>
        /// Mark entity as deleted in database
        /// </summary>
        /// <param name="id">Entity id</param>
        Task DeleteAsync(TId id);

        /// <summary>
        /// Upsert an entity
        /// </summary>
        /// <param name="filter">Filter for upserting</param>
        /// <param name="entity">Entity instance</param>
        Task UpsertAsync(TFilter filter, TDocument entity);

        /// <summary>
        /// Commit data
        /// </summary>
        /// <returns>Amount of changed data</returns>
        Task<int> CommitAsync();
    }
}

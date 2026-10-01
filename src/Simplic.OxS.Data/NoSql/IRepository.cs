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
        /// <param name="ct">Cancellation token</param>
        Task CreateAsync(TDocument entity, CancellationToken ct = default);

        /// <summary>
        /// Update an entity in the database
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="ct">Cancellation token</param>
        Task UpdateAsync(TDocument obj, CancellationToken ct = default);

        /// <summary>
        /// Mark entity as deleted in database
        /// </summary>
        /// <param name="id">Entity id</param>
        /// <param name="ct">Cancellation token</param>
        Task DeleteAsync(TId id, CancellationToken ct = default);

        /// <summary>
        /// Upsert an entity
        /// </summary>
        /// <param name="filter">Filter for upserting</param>
        /// <param name="entity">Entity instance</param>
        /// <param name="ct">Cancellation token</param>
        Task UpsertAsync(TFilter filter, TDocument entity, CancellationToken ct = default);

        /// <summary>
        /// Commit data
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Amount of changed data</returns>
        Task<int> CommitAsync(CancellationToken ct = default);
    }
}

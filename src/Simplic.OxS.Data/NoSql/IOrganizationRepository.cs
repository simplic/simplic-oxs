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
        /// <param name="ct">Cancellation token</param>
        /// <returns>Entity</returns>
        /// <remarks>
        /// <paramref name="queryAllOrganizations"/> has no default value on purpose: with a defaulted
        /// <paramref name="ct"/> on both overloads, <c>GetAsync(id)</c> would otherwise be ambiguous.
        /// </remarks>
        Task<TDocument> GetAsync(TId id, bool queryAllOrganizations, CancellationToken ct = default);

        /// <summary>
        /// Gets the collection of an document as an queryable.
        /// </summary>
        /// <returns></returns>
		Task<IExecutable<TDocument>> GetCollection();
	}
}

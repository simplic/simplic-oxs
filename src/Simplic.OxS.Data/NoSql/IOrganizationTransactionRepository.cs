namespace Simplic.OxS.Data
{
    /// <summary>
    /// Basic repository
    /// </summary>
    /// <typeparam name="TId">PK (ID) type</typeparam>
    /// <typeparam name="TDocument">Entity type</typeparam>
    /// <typeparam name="TFilter">Filter type</typeparam>
    public interface IOrganizationTransactionRepository<TId, TDocument, TFilter> : IRepository<TId, TDocument, TFilter>, ITransactionRepository<TDocument, TId>
        where TDocument : IOrganizationDocument<TId>, new ()
        where TFilter : IOrganizationFilter<TId>
    {
        /// <summary>
        /// Get an entity by its id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="queryAllOrganizations"></param>
        /// <returns>Entity</returns>
        Task<TDocument> GetAsync(TId id, bool queryAllOrganizations = false);
    }
}

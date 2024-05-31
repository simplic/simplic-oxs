using System.Threading.Tasks;
using MongoDB.Driver;
using Simplic.OxS.Data;

namespace Simplic.OxS.Data.MongoDB
{
    /// <summary>
    /// Base repository for reading and writing data from and to the mongo db
    /// </summary>
    /// <typeparam name="TId">Type of the key</typeparam>
    /// <typeparam name="TDocument">Document type</typeparam>
    /// <typeparam name="TFilter">Filter type</typeparam>
    public abstract class MongoRepositoryBase<TId, TDocument, TFilter> : MongoReadOnlyRepositoryBase<TId, TDocument, TFilter>, IRepository<TId, TDocument, TFilter>
        where TDocument : IDocument<TId>
        where TFilter : IFilter<TId>, new()
    {
        /// <summary>
        /// Initialize base repository
        /// </summary>
        /// <param name="context"></param>
        protected MongoRepositoryBase(IMongoContext context) : base(context)
        {
        }

        protected MongoRepositoryBase(IMongoContext context, string configurationKey) : base(context, configurationKey)
        {
        }

        /// <summary>
        /// Create new entity
        /// </summary>
        /// <param name="entity">Entity to create</param>
        public virtual async Task CreateAsync(TDocument document)
        {
            await Initialize();
            Context.AddCommand(() => Collection.InsertOneAsync(document));
        }

        /// <summary>
        /// Update an entity in the database
        /// </summary>
        /// <param name="obj"></param>
        public virtual async Task UpdateAsync(TDocument document)
        {
            await Initialize();
            Context.AddCommand(() => Collection.ReplaceOneAsync(GetFilterById(document.Id), document));
        }

        /// <summary>
        /// Mark entity as deleted in database
        /// </summary>
        /// <param name="id">Entity id</param>
        public virtual async Task DeleteAsync(TId id)
        {
            await Initialize();

            var document = await GetAsync(id);
            if (document != null)
            {
                document.IsDeleted = true;
                await UpdateAsync(document);
            }
        }

        /// <summary>
        /// Create or replace entity
        /// </summary>
        /// <param name="filter">Filter instance</param>
        /// <param name="entity">Entity instance</param>
        public async Task UpsertAsync(TFilter filter, TDocument entity)
        {
            await Initialize();

            var filterQuery = BuildFilterQuery(filter);

            Context.AddCommand(() => Collection.ReplaceOneAsync(filterQuery, entity, new ReplaceOptions { IsUpsert = true }));
        }

        /// <summary>
        /// Commit data
        /// </summary>
        /// <returns>Amount of changed data</returns>
        public virtual async Task<int> CommitAsync()
        {
            return await Context.SaveChangesAsync();
        }

        /// <summary>
        /// Gets base filter using an entity id
        /// </summary>
        /// <param name="id">Unique entity id</param>
        /// <returns>Filter instance</returns>
        protected FilterDefinition<TDocument> GetFilterById(TId id)
        {
            return BuildFilterQuery(new TFilter
            {
                Id = id
            });
        }

        /// <summary>
        /// Adds creation of a new entity to transaction.
        /// </summary>
        /// <param name="document">Entity to add.</param>
        /// <param name="transaction">Transaction.</param>
        /// <returns></returns>
        public virtual async Task CreateAsync(TDocument document, ITransaction transaction)
        {
            await Initialize();

            if (transaction == null)
                throw new System.ArgumentNullException(nameof(transaction));

            if (transaction is MongoTransaction mongoTransaction)
                await Collection.InsertOneAsync(mongoTransaction.Session, document);
            else
                throw new System.Exception($"Transaction is no of type {typeof(MongoTransaction).FullName}.");
        }

        /// <summary>
        /// Adds update of an entity into transaction.
        /// </summary>
        /// <param name="document"></param>
        /// <param name="transaction"></param>
        public virtual async Task UpdateAsync(TDocument document, ITransaction transaction)
        {
            await Initialize();

            if (transaction == null)
                throw new System.ArgumentNullException(nameof(transaction));

            if (transaction is MongoTransaction mongoTransaction)
                await Collection.ReplaceOneAsync(
                    mongoTransaction.Session, // Ensure the session is passed here
                    GetFilterById(document.Id),
                    document
                );
            else
                throw new System.Exception($"Transaction is no of type {typeof(MongoTransaction).FullName}.");
        }

        /// <summary>
        /// Adds deletion of an entity into transaction.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="transaction"></param>
        public virtual async Task DeleteAsync(TId id, ITransaction transaction)
        {
            await Initialize();

            if (transaction == null)
                throw new System.ArgumentNullException(nameof(transaction));

            if (transaction is MongoTransaction mongoTransaction)
            {
                var document = await GetAsync(id);
                if (document != null)
                {
                    document.IsDeleted = true;
                    await UpdateAsync(document, transaction);
                }
            }
            else
                throw new System.Exception($"Transaction is no of type {typeof(MongoTransaction).FullName}.");
        }
    }
}

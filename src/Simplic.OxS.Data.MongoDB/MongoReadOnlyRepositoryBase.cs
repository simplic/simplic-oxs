using MongoDB.Driver;
using Simplic.OxS.Security;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;

namespace Simplic.OxS.Data.MongoDB
{
    public abstract class MongoReadOnlyRepositoryBase<TId, TDocument, TFilter> : IReadOnlyRepository<TId, TDocument, TFilter>
        where TDocument : IDocument<TId>
        where TFilter : IFilter<TId>, new()
    {
        /// <summary>
        /// Field for caching resource urn.
        /// </summary>
        private static string? _resourceUrn = null;

        protected readonly IMongoContext Context;
        protected IMongoCollection<TDocument> Collection;

        protected MongoReadOnlyRepositoryBase(IMongoContext context)
        {
            Context = context;
        }

        protected MongoReadOnlyRepositoryBase(IMongoContext context, string configurationKey)
        {
            Context = context;
            context.SetConfiguration(configurationKey);
        }

        protected async Task Initialize()
        {
            if (Collection == null)
            {
                Collection = Context.GetCollection<TDocument>(GetCollectionName());
            }

            await Task.CompletedTask; // TODO change Initialize signature and all call sites to sync versions
        }

        protected abstract string GetCollectionName();

        public virtual async Task<TDocument> GetAsync(TId id)
        {
            await Initialize();

            var data = await GetByFilterAsync(new TFilter { Id = id });

            return data.SingleOrDefault();
        }

        public virtual async Task<IEnumerable<TDocument>> GetByFilterAsync(TFilter filter)
        {
            await Initialize();

            return (await Collection.FindAsync(BuildFilterQuery(filter)))
                .ToEnumerable();
        }

        public void Dispose()
        {
            Context?.Dispose();
        }

        protected virtual IEnumerable<FilterDefinition<TDocument>> GetFilterQueries(TFilter filter)
        {
            return new List<FilterDefinition<TDocument>>();
        }

        protected FilterDefinition<TDocument> BuildFilterQuery(TFilter filter)
        {
            var filterQueries = GetFilterQueries(filter).ToList();
            var builder = Builders<TDocument>.Filter;

            // compare reference types with null and value types with defaults
            // https://stackoverflow.com/a/864860/4315106
            var isIdHasValue = !EqualityComparer<TId>.Default.Equals(filter.Id, default);
            if (isIdHasValue)
            {
                filterQueries.Add(builder.Eq(d => d.Id, filter.Id));
            }

            if (filter.IsDeleted != null)
                filterQueries.Add(builder.Eq(d => d.IsDeleted, filter.IsDeleted));

            return filterQueries.Any()
                ? builder.And(filterQueries)
                : builder.Empty;
        }

        /// <summary>
        /// Finds the documents matching the filter.
        /// </summary>
        /// <param name="predicate">The filter predicate</param>
        /// <param name="skip">Number of skipped entities</param>
        /// <param name="limit">Number of requested entities</param>
        /// <param name="sortField">Sort field</param>
        /// <param name="isAscending">Ascending or Descending sort</param>
        /// <returns><see cref="TDocument"/> entities matching the search criteria</returns>
        public virtual async Task<IEnumerable<TDocument>> FindAsync(TFilter predicate, int? skip, int? limit, string sortField = "", bool isAscending = true, Collation collation = null)
        {
            await Initialize();

            SortDefinition<TDocument> sort = null;
            if (!string.IsNullOrWhiteSpace(sortField))
                sort = isAscending
                    ? Builders<TDocument>.Sort.Ascending(sortField)
                    : Builders<TDocument>.Sort.Descending(sortField);
            var findOptions = new FindOptions();
            if (collation != null)
                findOptions.Collation = collation;
            return Collection.Find(BuildFilterQuery(predicate), findOptions).Sort(sort).Skip(skip).Limit(limit).ToList();
        }

        /// <summary>
        /// Returns count of expected documents
        /// </summary>
        /// <param name="predicate">The filter predicate</param>
        /// <param name="collation">Collation options</param>
        /// <returns>Number of expected elements</returns>
        public virtual async Task<long> CountAsync(TFilter predicate, Collation collation = null)
        {
            await Initialize();

            var countOption = new CountOptions();

            if (collation != null)
                countOption.Collation = collation;

            return await Collection.CountDocumentsAsync(BuildFilterQuery(predicate), countOption);
        }

        /// <inheritdoc />
        public string? ResourceUrn
        {
            get
            {
                if (_resourceUrn == "")
                    return null;

                if (_resourceUrn == null)
                {
                    var attr = (ResourceActionsAttribute?)Attribute.GetCustomAttribute(
                        GetType(), typeof(ResourceActionsAttribute));

                    if (attr == null)
                    {
                        _resourceUrn = "";
                        return null;
                    }

                    _resourceUrn = attr.Name;
                }

                return _resourceUrn;
            }
        }
    }
}

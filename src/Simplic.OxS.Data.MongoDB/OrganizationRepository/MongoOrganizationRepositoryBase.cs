using HotChocolate;
using HotChocolate.Data;
using MongoDB.Driver;

namespace Simplic.OxS.Data.MongoDB
{
    public abstract class MongoOrganizationRepositoryBase<TDocument, TFilter> : MongoRepositoryBase<Guid, TDocument, TFilter>, IOrganizationRepository<Guid, TDocument, TFilter>
        where TDocument : IOrganizationDocument<Guid>
        where TFilter : IOrganizationFilter<Guid>, new() 
    {
        private readonly IMongoContext context;
        private readonly IRequestContext requestContext;

        protected MongoOrganizationRepositoryBase(IMongoContext context, IRequestContext requestContext) : base(context)
        {
            this.context = context;
            this.requestContext = requestContext;
        }

        public override async Task<TDocument> GetAsync(Guid id, CancellationToken ct = default)
        {
            return await GetAsync(id, false, ct);
        }

        public async Task<TDocument> GetAsync(Guid id, bool queryAllOrganizations, CancellationToken ct = default)
        {
            await Initialize();

            var data = await GetByFilterAsync(new TFilter
            {
                Id = id,
                QueryAllOrganizations = queryAllOrganizations
            }, ct);

            return data.SingleOrDefault();
        }

        /// <summary>
        /// Finds the documents matching the filter.
        /// </summary>
        /// <param name="predicate">The filter predicate</param>
        /// <param name="skip">Number of skipped entities</param>
        /// <param name="limit">Number of requested entities</param>
        /// <param name="sortField">Sort field</param>
        /// <param name="isAscending">Ascending or Descending sort</param>
        /// <param name="collation">Collation options</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns><see cref="TDocument"/> entities matching the search criteria</returns>
        public async override Task<IEnumerable<TDocument>> FindAsync(TFilter predicate, int? skip, int? limit, string sortField = "", bool isAscending = true, Collation collation = null, CancellationToken ct = default)
        {
            await Initialize();

            predicate.OrganizationId = predicate.OrganizationId.HasValue
                 ? predicate.OrganizationId
                 : predicate.QueryAllOrganizations
                    ? null
                    : requestContext.OrganizationId 
                        // Using an empty guid to prevent reading data from all Organizations.
                        ?? Guid.Empty;

            SortDefinition<TDocument> sort = null;
            if (!string.IsNullOrWhiteSpace(sortField))
                sort = isAscending
                    ? Builders<TDocument>.Sort.Ascending(sortField)
                    : Builders<TDocument>.Sort.Descending(sortField);

            var findOptions = new FindOptions();
            if (collation != null)
                findOptions.Collation = collation;

            return await Collection.Find(BuildFilterQuery(predicate), findOptions).Sort(sort).Skip(skip).Limit(limit).ToListAsync(ct);
        }

        public async Task<IEnumerable<TDocument>> GetAllAsync(CancellationToken ct = default)
        {
            return await GetAllAsync(false, ct);
        }

        public async Task<IEnumerable<TDocument>> GetAllAsync(bool queryAllOrganizations, CancellationToken ct = default)
        {
            return await GetByFilterAsync(new TFilter
            {
                QueryAllOrganizations = queryAllOrganizations,
                IsDeleted = false
            }, ct);
        }

        public override async Task<IEnumerable<TDocument>> GetByFilterAsync(TFilter filter, CancellationToken ct = default)
        {
            await Initialize();

            filter.OrganizationId = filter.OrganizationId.HasValue
                ? filter.OrganizationId
                : filter.QueryAllOrganizations
                    ? null
                    : requestContext.OrganizationId
                        // Using an empty guid to prevent reading data from all Organizations.
                        ?? Guid.Empty;

            return await Collection.Find(BuildFilterQuery(filter)).ToListAsync(ct);
        }

        private new FilterDefinition<TDocument> BuildFilterQuery(TFilter filter)
        {
            var filterQueries = GetFilterQueries(filter).ToList();
            var builder = Builders<TDocument>.Filter;

            if (filter.Id != Guid.Empty)
            {
                filterQueries.Add(builder.Eq(d => d.Id, filter.Id));
            }

            if (filter.OrganizationId.HasValue)
            {
                filterQueries.Add(builder.Eq(d => d.OrganizationId, filter.OrganizationId));
            }

            if (filter.IsDeleted.HasValue)
            {
                filterQueries.Add(builder.Eq(d => d.IsDeleted, filter.IsDeleted));
            }

            if (filter.IncludeIds != null)
            {
                filterQueries.Add(builder.In(o => o.Id, filter.IncludeIds));
            }

            if (filter.ExcludeId.HasValue)
            {
                filterQueries.Add(builder.Ne(d => d.Id, filter.ExcludeId));
            }

            return filterQueries.Any()
                ? builder.And(filterQueries)
                : builder.Empty;
        }

        /// <inheritdoc/>
        public async Task<IExecutable<TDocument>> GetCollection()
        {
            return context.GetCollection<TDocument>(GetCollectionName()).Find(x => x.OrganizationId == requestContext.OrganizationId).AsExecutable();
        }
    }

    public abstract class MongoOrganizationRepositoryBase<TDocument> : MongoOrganizationRepositoryBase<TDocument, OrganizationFilterBase>
        where TDocument : OrganizationDocumentBase
    {
        protected MongoOrganizationRepositoryBase(IMongoContext context, IRequestContext requestContext) : base(context, requestContext)
        {
        }
    }
}

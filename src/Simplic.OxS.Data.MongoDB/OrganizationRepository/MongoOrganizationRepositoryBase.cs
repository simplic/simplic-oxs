using HotChocolate;
using HotChocolate.Data;
using MongoDB.Driver;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.Data.MongoDB.Abac;

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

        /// <summary>
        /// Initializes the repository with an optional ABAC read-filter provider (§7.6).
        /// </summary>
        protected MongoOrganizationRepositoryBase(IMongoContext context, IRequestContext requestContext, IAbacReadFilterProvider? abacReadFilterProvider)
            : this(context, requestContext)
        {
            AbacReadFilterProvider = abacReadFilterProvider;
        }

        /// <summary>
        /// Opt-in ABAC read action (e.g. <c>oxs:contact:read</c>). When set and an
        /// <see cref="AbacReadFilterProvider"/> is available, all reads (<c>GetAsync</c>,
        /// <c>GetByFilterAsync</c>, <c>GetAllAsync</c>, <c>FindAsync</c>, <c>CountAsync</c>,
        /// <c>GetCollection</c>) AND the ABAC MongoDB filter of this action. A complete deny throws
        /// <see cref="AbacAccessDeniedException"/> (→ 403); a single document filtered out by resource
        /// conditions is returned as <c>null</c> (→ 404). Default: <c>null</c> (ABAC disabled).
        /// </summary>
        protected virtual string? AbacReadAction => null;

        /// <summary>
        /// Gets or sets the ABAC read-filter provider. Can be injected via the constructor overload
        /// or set as property. When <c>null</c>, no ABAC read filter is applied.
        /// </summary>
        public IAbacReadFilterProvider? AbacReadFilterProvider { get; set; }

        /// <summary>
        /// ANDs the ABAC read filter of <see cref="AbacReadAction"/> into <paramref name="filter"/>.
        /// Returns <paramref name="filter"/> unchanged when ABAC is not configured for this repository
        /// or suppressed via <see cref="AbacDataScope.SuppressReadFilter"/>.
        /// </summary>
        /// <exception cref="AbacAccessDeniedException">The read action is denied entirely.</exception>
        protected async Task<FilterDefinition<TDocument>> ApplyAbacReadFilterAsync(FilterDefinition<TDocument> filter, CancellationToken ct = default)
        {
            var action = AbacReadAction;
            if (string.IsNullOrWhiteSpace(action) || AbacReadFilterProvider == null || AbacDataScope.IsReadFilterSuppressed)
                return filter;

            var decision = await AbacReadFilterProvider.GetReadFilterAsync(action, ct);
            if (decision == null || decision.Decision != AbacDecision.Allow)
                throw new AbacAccessDeniedException(action);

            return filter.ApplyAbacFilter(decision.MongoDbFilterJson);
        }

        public override async Task<TDocument> GetAsync(Guid id)
        {
            return await GetAsync(id, false);
        }

        public async Task<TDocument> GetAsync(Guid id, bool queryAllOrganizations)
        {
            await Initialize();

            var data = await GetByFilterAsync(new TFilter
            {
                Id = id,
                QueryAllOrganizations = queryAllOrganizations
            });

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
        /// <returns><see cref="TDocument"/> entities matching the search criteria</returns>
        public async override Task<IEnumerable<TDocument>> FindAsync(TFilter predicate, int? skip, int? limit, string sortField = "", bool isAscending = true, Collation collation = null)
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

            var query = await ApplyAbacReadFilterAsync(BuildFilterQuery(predicate));

            return Collection.Find(query, findOptions).Sort(sort).Skip(skip).Limit(limit).ToList();
        }

        public async Task<IEnumerable<TDocument>> GetAllAsync()
        {
            return await GetAllAsync(false);
        }

        public async Task<IEnumerable<TDocument>> GetAllAsync(bool queryAllOrganizations)
        {
            return await GetByFilterAsync(new TFilter
            {
                QueryAllOrganizations = queryAllOrganizations,
                IsDeleted = false
            });
        }

        public override async Task<IEnumerable<TDocument>> GetByFilterAsync(TFilter filter)
        {
            await Initialize();

            filter.OrganizationId = filter.OrganizationId.HasValue
                ? filter.OrganizationId
                : filter.QueryAllOrganizations
                    ? null
                    : requestContext.OrganizationId
                        // Using an empty guid to prevent reading data from all Organizations.
                        ?? Guid.Empty;

            var query = await ApplyAbacReadFilterAsync(BuildFilterQuery(filter));

            return (await Collection.FindAsync(query))
                    .ToEnumerable();
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
            var filter = await ApplyAbacReadFilterAsync(
                Builders<TDocument>.Filter.Where(x => x.OrganizationId == requestContext.OrganizationId));

            return context.GetCollection<TDocument>(GetCollectionName()).Find(filter).AsExecutable();
        }

        /// <summary>
        /// Returns the count of documents matching the filter, scoped to the organization like
        /// <c>FindAsync</c>/<c>GetByFilterAsync</c>. When an ABAC read action is configured,
        /// the ABAC read filter is applied as well.
        /// </summary>
        /// <param name="predicate">The filter predicate</param>
        /// <param name="collation">Collation options</param>
        /// <returns>Number of expected elements</returns>
        public override async Task<long> CountAsync(TFilter predicate, Collation collation = null)
        {
            await Initialize();

            var countOption = new CountOptions();
            if (collation != null)
                countOption.Collation = collation;

            predicate.OrganizationId = predicate.OrganizationId.HasValue
                ? predicate.OrganizationId
                : predicate.QueryAllOrganizations
                    ? null
                    : requestContext.OrganizationId
                        // Using an empty guid to prevent reading data from all Organizations.
                        ?? Guid.Empty;

            var query = await ApplyAbacReadFilterAsync(BuildFilterQuery(predicate));
            return await Collection.CountDocumentsAsync(query, countOption);
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

using MongoDB.Driver;

namespace Simplic.OxS.Data.MongoDB
{
    public abstract class MongoOrganizationRepositoryBase<TDocument, TFilter> : MongoRepositoryBase<Guid, TDocument, TFilter>, IOrganizationRepository<Guid, TDocument, TFilter>
        where TDocument : IOrganizationDocument<Guid>
        where TFilter : IOrganizationFilter<Guid>, new() 
    {
        private readonly IRequestContext requestContext;

        protected MongoOrganizationRepositoryBase(IMongoContext context, IRequestContext requestContext) : base(context)
        {
            this.requestContext = requestContext;
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

        public async Task<IEnumerable<TDocument>> GetAllAsync()
        {
            return await GetAllAsync(false);
        }

        public async Task<IEnumerable<TDocument>> GetAllAsync(bool queryAllOrganizations)
        {
            return await GetByFilterAsync(new TFilter
            {
                QueryAllOrganizations = queryAllOrganizations
            });
        }

        public override async Task<IEnumerable<TDocument>> GetByFilterAsync(TFilter filter)
        {
            await Initialize();

            filter.OrganizationId = filter.OrganizationId.HasValue
                ? filter.OrganizationId
                : filter.QueryAllOrganizations
                    ? null
                    : requestContext.OrganizationId;

            return (await Collection.FindAsync(BuildFilterQuery(filter)))
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
    }

    public abstract class MongoOrganizationRepositoryBase<TDocument> : MongoOrganizationRepositoryBase<TDocument, OrganizationFilterBase>
        where TDocument : OrganizationDocumentBase
    {
        protected MongoOrganizationRepositoryBase(IMongoContext context, IRequestContext requestContext) : base(context, requestContext)
        {
        }
    }
}

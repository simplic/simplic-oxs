using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Data.Service
{
    public class OrganizationServiceBase<TDocument, TFilter> : IOrganizationServiceBase<TDocument, TFilter> where TDocument : IOrganizationDocument<Guid>
                                                                                                            where TFilter : IOrganizationFilter<Guid>
    {
        private readonly IOrganizationRepository<Guid, TDocument, TFilter> repository;
        private readonly IRequestContext requestContext;

        public OrganizationServiceBase(IOrganizationRepository<Guid, TDocument, TFilter> repository, IRequestContext requestContext)
        {
            this.repository = repository;
            this.requestContext = requestContext;
        }

        public virtual async Task<TDocument> GetById(Guid id) => await repository.GetAsync(id);

        public virtual async Task<TDocument> Create([NotNull] TDocument obj)
        {
            AssertRequest(obj, false);

            if (obj.Id == default)
                obj.Id = Guid.NewGuid();

            obj.OrganizationId = requestContext.OrganizationId.Value;

            if (obj is IDocumentDataExtension ext)
            {
                ext.CreateDateTime = DateTime.UtcNow;
                ext.CreateUserId = requestContext.UserId;

                ext.UpdateDateTime = ext.CreateDateTime;
                ext.UpdateUserId = ext.CreateUserId;
            }

            await repository.CreateAsync(obj);
            await repository.CommitAsync();

            return obj;
        }

        public virtual async Task<TDocument> Update([NotNull] TDocument obj)
        {
            AssertRequest(obj, true);

            if (obj.Id == default)
                throw new Exception("Id must be set for updating data.");

            if (obj is IDocumentDataExtension ext)
            {
                ext.UpdateDateTime = DateTime.UtcNow;
                ext.UpdateUserId = requestContext.UserId;
            }

            await repository.UpdateAsync(obj);
            await repository.CommitAsync();

            return obj;
        }

        public virtual async Task<TDocument> Delete([NotNull] TDocument obj)
        {
            AssertRequest(obj, true);

            obj.IsDeleted = true;

            if (obj is IDocumentDataExtension ext)
            {
                ext.UpdateDateTime = DateTime.UtcNow;
                ext.UpdateUserId = requestContext.UserId;
            }

            await repository.UpdateAsync(obj);
            await repository.CommitAsync();

            return obj;
        }

        public virtual async Task Delete(Guid id)
        {
            var obj = await GetById(id);
            if (obj != null)
                await Delete(obj);
        }

        private void AssertRequest(TDocument obj, bool compareOrganizationId)
        {
            if (requestContext.OrganizationId == null)
                throw new Exception("OrganizationId is null for the actual request.");

            if (compareOrganizationId && obj.OrganizationId != requestContext.OrganizationId)
                throw new Exception("Invalid organization id, access the object is not allowed.");
        }
    }
}

﻿using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Data.Service
{
    public class OrganizationTransactionServiceBase<TDocument, TFilter> : OrganizationServiceBase<TDocument, TFilter>, IOrganizationTransactionServiceBase<TDocument, TFilter>
        where TDocument : IOrganizationDocument<Guid>, new ()
        where TFilter : IOrganizationFilter<Guid>
    {
        private readonly IOrganizationTransactionRepository<Guid, TDocument, TFilter> repository;
        private readonly IRequestContext requestContext;

        public OrganizationTransactionServiceBase(IOrganizationTransactionRepository<Guid, TDocument, TFilter> repository, IRequestContext requestContext) : base(repository, requestContext)
        {
            this.repository = repository;
            this.requestContext = requestContext;
        }

        public virtual async Task<TDocument> Create([NotNull] TDocument obj, ITransaction transaction)
        {
            AssertRequest(obj, false);

            obj.Id = Guid.NewGuid();
            obj.OrganizationId = requestContext.OrganizationId.Value;

            if (obj is IDocumentDataExtension ext)
            {
                ext.CreateDateTime = DateTime.UtcNow;
                ext.CreateUserId = requestContext.UserId;

                ext.UpdateDateTime = ext.CreateDateTime;
                ext.UpdateUserId = ext.CreateUserId;
            }

            await repository.CreateAsync(obj, transaction);

            return obj;
        }

        public virtual async Task<TDocument> Update([NotNull] TDocument obj, ITransaction transaction)
        {
            AssertRequest(obj, true);

            if (obj.Id == default)
                throw new Exception("Id must be set for updating data.");

            if (obj is IDocumentDataExtension ext)
            {
                ext.UpdateDateTime = DateTime.UtcNow;
                ext.UpdateUserId = requestContext.UserId;
            }

            await repository.UpdateAsync(obj, transaction);

            return obj;
        }

        public virtual async Task<TDocument> Delete([NotNull] TDocument obj, ITransaction transaction)
        {
            AssertRequest(obj, true);

            obj.IsDeleted = true;

            if (obj is IDocumentDataExtension ext)
            {
                ext.UpdateDateTime = DateTime.UtcNow;
                ext.UpdateUserId = requestContext.UserId;
            }

            await repository.UpdateAsync(obj, transaction);

            return obj;
        }

        public virtual async Task Delete(Guid id, ITransaction transaction)
        {
            var obj = await GetById(id);
            if (obj != null)
                await Delete(obj, transaction);
        }

        private void AssertRequest(TDocument obj, bool compareOrganizationId)
        {
            if (requestContext.OrganizationId == null)
                throw new Exception("OrganizationId is null for the current request.");

            if (compareOrganizationId && obj.OrganizationId != requestContext.OrganizationId)
                throw new Exception("Invalid organization ID, accessing the object is not allowed.");
        }
    }
}

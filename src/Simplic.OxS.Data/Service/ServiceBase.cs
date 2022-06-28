using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Data.Service
{
    public class ServiceBase<TDocument, TFilter> : IServiceBase<TDocument, TFilter> where TDocument : IDocument<Guid>
                                                                                    where TFilter : IFilter<Guid>
    {
        private readonly IRepository<Guid, TDocument, TFilter> repository;
        private readonly IRequestContext requestContext;

        public ServiceBase(IRepository<Guid, TDocument, TFilter> repository, IRequestContext requestContext)
        {
            this.repository = repository;
            this.requestContext = requestContext;
        }

        public virtual async Task<TDocument> GetById(Guid id) => await repository.GetAsync(id);

        public virtual async Task<TDocument> Create([NotNull] TDocument obj)
        {
            obj.Id = Guid.NewGuid();

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
    }
}

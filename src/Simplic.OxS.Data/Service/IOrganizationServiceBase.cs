using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Data.Service
{
    public interface IOrganizationServiceBase<TDocument, TFilter> where TDocument : IOrganizationDocument<Guid>
                                                                  where TFilter : IOrganizationFilter<Guid>
    {
        Task<TDocument> GetById(Guid id);

        Task<TDocument> Create([NotNull] TDocument obj);

        Task<TDocument> Update([NotNull] TDocument obj);

        Task<TDocument> Delete([NotNull] TDocument obj);

        Task Delete(Guid id);
    }
}

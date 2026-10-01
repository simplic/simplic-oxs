using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Data.Service
{
    public interface IOrganizationServiceBase<TDocument, TFilter> where TDocument : IOrganizationDocument<Guid>
                                                                  where TFilter : IOrganizationFilter<Guid>
    {
        Task<TDocument> GetById(Guid id, CancellationToken ct = default);

        Task<TDocument> Create([NotNull] TDocument obj, CancellationToken ct = default);

        Task<TDocument> Update([NotNull] TDocument obj, CancellationToken ct = default);

        Task<TDocument> Delete([NotNull] TDocument obj, CancellationToken ct = default);

        Task Delete(Guid id, CancellationToken ct = default);
    }
}

using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Data.Service
{
    public interface IOrganizationTransactionServiceBase<TDocument, TFilter> : IOrganizationServiceBase<TDocument, TFilter>
        where TDocument : IOrganizationDocument<Guid>
        where TFilter : IOrganizationFilter<Guid>
    {
        Task<TDocument> Create([NotNull] TDocument obj, ITransaction transaction, CancellationToken ct = default);

        Task<TDocument> Update([NotNull] TDocument obj, ITransaction transaction, CancellationToken ct = default);

        Task<TDocument> Delete([NotNull] TDocument obj, ITransaction transaction, CancellationToken ct = default);

        Task Delete(Guid id, ITransaction transaction, CancellationToken ct = default);
    }
}

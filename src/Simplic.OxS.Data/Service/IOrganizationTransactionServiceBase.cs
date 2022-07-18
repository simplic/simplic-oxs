using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Data.Service
{
    public interface IOrganizationTransactionServiceBase<TDocument, TFilter>
        where TDocument : IOrganizationDocument<Guid>
        where TFilter : IOrganizationFilter<Guid>
    {
        Task<TDocument> Create([NotNull] TDocument obj, ITransaction transaction);

        Task<TDocument> Update([NotNull] TDocument obj, ITransaction transaction);

        Task<TDocument> Delete([NotNull] TDocument obj, ITransaction transaction);

        Task Delete(Guid id, ITransaction transaction);
    }
}

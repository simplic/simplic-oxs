using System.Threading;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Interface for a transaction service.
    /// </summary>
    public interface ITransactionService
    {
        /// <summary>
        /// Asynchronously creates a new transaction.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task of transaction.</returns>
        Task<ITransaction> CreateAsync(CancellationToken ct = default);

        /// <summary>
        /// Asynchronously commits a transaction.
        /// </summary>
        /// <param name="transaction">Transaction to commit.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task CommitAsync(ITransaction transaction, CancellationToken ct = default);

        /// <summary>
        /// Asynchronously aborts a transaction.
        /// </summary>
        /// <param name="transaction">Transaction to abort.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task AbortAsync(ITransaction transaction, CancellationToken ct = default);
    }
}

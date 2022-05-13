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
        /// <returns>Task of transaction.</returns>
        Task<ITransaction> CreateAsync();

        /// <summary>
        /// Asynchronously commits a transaction.
        /// </summary>
        /// <param name="transaction">Transaction to commit.</param>
        /// <returns>Task.</returns>
        Task CommitAsync(ITransaction transaction);

        /// <summary>
        /// Asynchronously aborts a transaction.
        /// </summary>
        /// <param name="transaction">Transaction to abort.</param>
        /// <returns>Task.</returns>
        Task AbortAsync(ITransaction transaction);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Database context
    /// </summary>
    public interface IDatabaseContext : IDisposable
    {
        /// <summary>
        /// Add a command to the database context
        /// </summary>
        /// <param name="func">Command delegate. Receives the cancellation token passed to <see cref="SaveChangesAsync"/>.</param>
        void AddCommand(Func<CancellationToken, Task> func);

        /// <summary>
        /// Save changes in the current context
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Amount of changes</returns>
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}

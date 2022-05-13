using System;
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
        /// <param name="func">Command delegate</param>
        void AddCommand(Func<Task> func);

        /// <summary>
        /// Save changes in the current context
        /// </summary>
        /// <returns>Amount of changes</returns>
        Task<int> SaveChangesAsync();
    }
}

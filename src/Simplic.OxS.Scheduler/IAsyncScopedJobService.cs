using Microsoft.AspNetCore.Mvc.Filters;

namespace Simplic.OxS.Scheduler
{
    /// <summary>
    /// Needs to be implented for scoped jobs that contains async code
    /// </summary>
    public interface IAsyncScopedJobService
    {
        /// <summary>
        /// Execute async job
        /// </summary>
        Task ExecuteAsync();
    }
}
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
        /// <param name="parameter">Contains the scoped job parameter</param>
        Task ExecuteAsync(ScopedJobParameter parameter);
    }
}
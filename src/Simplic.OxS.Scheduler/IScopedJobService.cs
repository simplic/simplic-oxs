using Microsoft.AspNetCore.Mvc.Filters;

namespace Simplic.OxS.Scheduler
{
    /// <summary>
    /// Needs to be implented for scoped jobs
    /// </summary>
    public interface IScopedJobService
    {
        /// <summary>
        /// Execute job
        /// </summary>
        void Execute();
    }
}
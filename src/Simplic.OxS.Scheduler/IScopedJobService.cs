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
        /// <param name="parameter">Contains the scoped job parameter</param>
        void Execute(ScopedJobParameter parameter);
    }
}
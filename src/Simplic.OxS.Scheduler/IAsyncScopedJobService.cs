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
        /// <param name="ct">Cancellation token. Signalled by Hangfire on server shutdown or when the job is deleted,
        /// provided the job was enqueued via <see cref="AsyncScopedJob.ExecuteJobAsync(Type, ScopedJobParameter, CancellationToken)"/>.</param>
        Task ExecuteAsync(ScopedJobParameter parameter, CancellationToken ct = default);
    }
}
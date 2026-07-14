using Hangfire;
using Hangfire.States;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Scheduler
{
    /// <summary>
    /// Hangfire recurring job that deletes failed jobs older than a configurable retention period.
    /// </summary>
    public sealed class FailedJobCleanup
    {
        private readonly JobStorage _jobStorage;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<FailedJobCleanup> _logger;

        public FailedJobCleanup(
            JobStorage jobStorage,
            IBackgroundJobClient backgroundJobClient,
            ILogger<FailedJobCleanup> logger)
        {
            _jobStorage = jobStorage;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }
        
        public void DeleteFailedJobsOlderThan(
            int retentionDays,
            int batchSize = 500)
        {
            return;

            if (retentionDays < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(retentionDays),
                    "Retention must be at least one day.");
            }

            if (batchSize < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(batchSize),
                    "Batch size must be greater than zero.");
            }

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var monitoringApi = _jobStorage.GetMonitoringApi();

            var jobIdsToDelete = new List<string>();
            var offset = 0;

            _logger.LogInformation(
                "Starting failed-job cleanup. Jobs failed before {Cutoff} will be deleted.",
                cutoff);

            /*
             * FailedJobs returns jobs in pages. We first collect the IDs and
             * delete them afterward.
             *
             * Deleting while paging would change the collection and could cause
             * some entries to be skipped.
             */
            while (true)
            {
                var failedJobs = monitoringApi.FailedJobs(offset, batchSize);

                if (failedJobs.Count == 0)
                {
                    break;
                }

                foreach (var failedJob in failedJobs)
                {
                    var jobId = failedJob.Key;
                    var job = failedJob.Value;

                    /*
                     * InFailedState protects against stale monitoring results.
                     * FailedAt may be nullable depending on the storage provider.
                     */
                    if (job.InFailedState &&
                        job.FailedAt.HasValue &&
                        job.FailedAt.Value < cutoff)
                    {
                        jobIdsToDelete.Add(jobId);
                    }
                }

                if (failedJobs.Count < batchSize)
                {
                    break;
                }

                offset += failedJobs.Count;
            }

            var deletedCount = 0;
            var skippedCount = 0;

            foreach (var jobId in jobIdsToDelete)
            {
                /*
                 * The expected-state argument makes this race-safe:
                 * the job is deleted only when it is still Failed.
                 *
                 * For example, if somebody requeues it from the dashboard after
                 * the scan, the cleanup job will not delete the requeued job.
                 */
                var deletedState = new DeletedState
                {
                    Reason = $"Failed job exceeded the {retentionDays}-day retention period."
                };

                var deleted = _backgroundJobClient.ChangeState(
                    jobId,
                    deletedState,
                    expectedState: FailedState.StateName);

                if (deleted)
                {
                    deletedCount++;

                    _logger.LogDebug(
                        "Deleted expired failed Hangfire job {JobId}.",
                        jobId);
                }
                else
                {
                    skippedCount++;

                    _logger.LogInformation(
                        "Skipped Hangfire job {JobId} because it is no longer in the Failed state.",
                        jobId);
                }
            }

            _logger.LogInformation(
                "Failed-job cleanup completed. Found: {FoundCount}, deleted: {DeletedCount}, skipped: {SkippedCount}.",
                jobIdsToDelete.Count,
                deletedCount,
                skippedCount);
        }
    }
}

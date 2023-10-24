using Hangfire;
using Simplic.OxS.Scheduler;

namespace Simplic.OxS.HangfireSample
{
    [Queue("sample")]
    public class SampleAsyncJob : IAsyncScopedJobService
    {
        private readonly ILogger<SampleJob> logger;
        private readonly IRequestContext requestContext;

        public SampleAsyncJob(ILogger<SampleJob> logger, IRequestContext requestContext, IServiceProvider sp)
        {
            this.logger = logger;
            this.requestContext = requestContext;
        }

        public async Task ExecuteAsync()
        {
            logger.LogWarning($"ASYNC! Executing job with data: {requestContext.CorrelationId}");
            logger.LogWarning($" User: {requestContext.UserId}");
            logger.LogWarning($" Organization: {requestContext.OrganizationId}");

            // Execute your job logic here
            await Task.Delay(1000);
        }
    }
}

using Hangfire;
using Simplic.OxS.Scheduler;

namespace Simplic.OxS.HangfireSample
{
    [Queue("sample")]
    public class SampleJob : IScopedJobService
    {
        private readonly ILogger<SampleJob> logger;
        private readonly IRequestContext requestContext;

        public SampleJob(ILogger<SampleJob> logger, IRequestContext requestContext, IServiceProvider sp)
        {
            this.logger = logger;
            this.requestContext = requestContext;
        }

        public void Execute()
        {
            logger.LogInformation($"Executing job with data: {requestContext.CorrelationId}");
            logger.LogInformation($" User: {requestContext.UserId}");
            logger.LogInformation($" Organization: {requestContext.OrganizationId}");

            // Execute your job logic here
        }
    }
}

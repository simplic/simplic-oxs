using Hangfire;
using Hangfire.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Scheduler
{
    public class JobWithRequestContextActivator : AspNetCoreJobActivator
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public JobWithRequestContextActivator([NotNull] IServiceScopeFactory serviceScopeFactory) : base(serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        }

        public override JobActivatorScope BeginScope(JobActivatorContext context)
        {
            var requestContext = context.GetJobParameter<IRequestContext>("RequestContext");

            if (requestContext == null)
            {
                return base.BeginScope(context);
            }

            var serviceScope = _serviceScopeFactory.CreateScope();

            var userContextForJob = serviceScope.ServiceProvider.GetRequiredService<IRequestContext>();
            userContextForJob.OrganizationId = requestContext.OrganizationId;
            userContextForJob.CorrelationId = Guid.NewGuid();
            userContextForJob.UserId = requestContext.UserId;

            return new ServiceJobActivatorScope(serviceScope);
        }
    }
}
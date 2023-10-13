using Hangfire;
using Hangfire.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Scheduler
{
    internal class JobWithRequestContextActivator : AspNetCoreJobActivator
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

            var userContextForJob = serviceScope.ServiceProvider.GetRequiredService<IJobContextAccessor>();
            userContextForJob.Context = new JobWithRequestContext { ReqeustContext = requestContext };

            return new ServiceJobActivatorScope(serviceScope);
        }
    }
}
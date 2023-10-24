using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using static System.Formats.Asn1.AsnWriter;

namespace Simplic.OxS.Scheduler
{
    public class ScopedJob : IDisposable
    {
        private readonly IServiceScope serviceScope;
        private readonly IRequestContext requestContext;

        public ScopedJob([NotNull] IServiceScopeFactory serviceScopeFactory)
        {
            serviceScope = serviceScopeFactory.CreateScope();

            requestContext = serviceScope.ServiceProvider.GetService<IRequestContext>();
        }

        public void ExecuteJob(Type jobService, ScopeJobParameter parameter)
        {
            // Set requestContext data for the current scope
            requestContext!.CorrelationId = Guid.NewGuid();

            if (parameter != null)
            {
                requestContext.OrganizationId = parameter.OrganizationId;
                requestContext.UserId = parameter.UserId;
            }

            var service = serviceScope.ServiceProvider.GetService(jobService);

            if (service is IScopedJobService scopedService)
            {
                scopedService.Execute();
            }
            else
            {
                throw new Exception($"Service is not registered: {jobService.FullName}. Please register the service using `service.AddTransient<>();`");
            }
        }

        public void Dispose()
        {
            serviceScope.Dispose();
        }

        public string Name { get; set; }

        public IRequestContext RequestContext { get => requestContext; }
    }
}

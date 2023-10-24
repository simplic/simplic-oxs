using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Scheduler
{
    public class AsyncScopedJob : IDisposable
    {
        private readonly IServiceScope serviceScope;
        private readonly IRequestContext requestContext;

        public AsyncScopedJob([NotNull] IServiceScopeFactory serviceScopeFactory)
        {
            serviceScope = serviceScopeFactory.CreateScope();

            requestContext = serviceScope.ServiceProvider.GetService<IRequestContext>();
        }

        public async Task ExecuteJobAsync(Type jobService, ScopeJobParameter parameter)
        {
            // Set requestContext data for the current scope
            requestContext!.CorrelationId = Guid.NewGuid();

            if (parameter != null)
            {
                requestContext.OrganizationId = parameter.OrganizationId;
                requestContext.UserId = parameter.UserId;
            }

            var service = serviceScope.ServiceProvider.GetService(jobService);

            if (service is IAsyncScopedJobService scopedService)
            {
                await scopedService.ExecuteAsync();
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

using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Scheduler
{
    /// <summary>
    /// Hangfire job that execute a <see cref="IAsyncScopedJobService"/> and creates its own DI scope
    /// </summary>
    public class AsyncScopedJob : IDisposable
    {
        private readonly IServiceScope serviceScope;
        private readonly IRequestContext requestContext;

        /// <summary>
        /// Initialize new job and create new DI scope
        /// </summary>
        /// <param name="serviceScopeFactory">Scope factory for creating a new DI-scope</param>
        public AsyncScopedJob([NotNull] IServiceScopeFactory serviceScopeFactory)
        {
            serviceScope = serviceScopeFactory.CreateScope();

            requestContext = serviceScope.ServiceProvider.GetService<IRequestContext>();
        }

        /// <summary>
        /// Execute job async
        /// </summary>
        /// <param name="jobService">Job to execute as type. The type must be registered using services.AddTransient before.</param>
        /// <param name="parameter">Parameter for passing e.g. user-id, organization-id, etv.</param>
        /// <exception cref="Exception">Throws an exception, if the jobService is not registered</exception>
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

        /// <summary>
        /// Dispose object and clear scope
        /// </summary>
        public void Dispose()
        {
            serviceScope.Dispose();
        }
    }
}

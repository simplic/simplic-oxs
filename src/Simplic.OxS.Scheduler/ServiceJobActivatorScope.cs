using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Simplic.OxS.Scheduler
{
    public class ServiceJobActivatorScope : JobActivatorScope
    {
        private IServiceScope serviceScope;

        public ServiceJobActivatorScope(IServiceScope serviceScope)
        {
            this.serviceScope = serviceScope;
        }

        public override object Resolve(Type type)
        {
            return ActivatorUtilities.CreateInstance(serviceScope.ServiceProvider, type);
        }

        public override void DisposeScope()
        {
            serviceScope.Dispose();
        }
    }
}
using Simplic.OxS.MessageBroker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Server;

namespace Simplic.OxS.Sms
{
    public class Startup : Bootstrap
    {
        public Startup(IConfiguration configuration) : base(configuration)
        {

        }

        protected override void ConfigureEndpointConventions(IServiceCollection services, MessageBrokerSettings settings)
        {

        }

        protected override void RegisterServices(IServiceCollection services)
        {

        }

        protected override string ServiceName => "Sms";
    }
}

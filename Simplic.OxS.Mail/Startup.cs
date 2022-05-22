using Simplic.OxS.MessageBroker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Server;

namespace Simplic.OxS.Mail
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
            sib_api_v3_sdk.Client.Configuration.Default.ApiKey.Add("api-key", "");
        }

        protected override string ServiceName => "Mail";
    }
}

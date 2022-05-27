using MassTransit;
using Simplic.OxS.Auth.Data.MongoDB;
using Simplic.OxS.Auth.Service;
using Simplic.OxS.MessageBroker;
using Simplic.OxS.Server;

namespace Simplic.OxS.Auth.Server
{
    public class Startup : Bootstrap
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment currentEnvironment) : base(configuration, currentEnvironment)
        {

        }

        protected override void ConfigureEndpointConventions(IServiceCollection services, MessageBrokerSettings settings)
        {
            // EndpointConvention.Map<SchemaRegistry.SendMailCommand>(new Uri(settings.Host + "simplic.oxs.send_mail.command"));
            // EndpointConvention.Map<SchemaRegistry.SendSmsCommand>(new Uri(settings.Host + "simplic.oxs.send_sms.command"));
            // EndpointConvention.Map<SchemaRegistry.AddNewsEntryCommand>(new Uri(settings.Host + "simplic.oxs.add_news_entry.command"));
        }

        protected override void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<ITwoFactorTokenRepository, TwoFactorTokenRepository>();
            services.AddTransient<ITwoFactorTokenService, TwoFactorTokenService>();

            services.AddTransient<IDatabaseMigrationService, Migration.AuthMigrationService>();
        }

        protected override string ServiceName => "Auth";
    }
}

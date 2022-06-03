using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Data.MongoDB;

namespace Simplic.OxS.Server.Extensions
{
    internal static class MongoDbExtension
    {
        internal static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ConnectionSettings>(options => configuration.GetSection("MongoDB").Bind(options));

            // Setup database stuff
            var databaseSection = configuration.GetSection("MongoDB");
            if (databaseSection != null)
            {
                Console.WriteLine(" > Add MongoDB context");
                services.AddTransient<IMongoContext, MongoContext>();
            }
            else
            {
                Console.WriteLine(" > NO MongoDB context found.");
            }

            return services;
        }
    }
}

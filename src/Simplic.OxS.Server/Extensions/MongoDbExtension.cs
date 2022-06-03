using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Data.MongoDB;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// MongoDb service extension
    /// </summary>
    internal static class MongoDbExtension
    {
        /// <summary>
        /// Adds the mongodb extension
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Service configuration</param>
        /// <returns>Service collection instance</returns>
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

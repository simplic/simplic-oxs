using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
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
            RegisterGuidSerializer();

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

        /// <summary>
        /// Registers the serializer that stores a guid in the standard representation. The
        /// registration is the process's, so a second host of the process (or a schema check
        /// before a host) meets it in place and leaves it; a different guid serializer registered
        /// before it still throws, because a host must not run on another representation.
        /// </summary>
        internal static void RegisterGuidSerializer() =>
            MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(new MongoDB.Bson.Serialization.Serializers.GuidSerializer(GuidRepresentation.Standard));
    }
}

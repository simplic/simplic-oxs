using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Simplic.OxS.ResourceLocking
{
    /// <summary>
    /// Allows adding resource locking to services.
    /// </summary>
    public static class ResourceLockingExtension
    {
        /// <summary>
        /// Adds resource locking service to services.
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration</param>
        /// <returns></returns>
        public static IServiceCollection AddResourceLocking(this IServiceCollection services, IConfiguration configuration)
        {
            var redisUrl = configuration.GetValue<string>("Redis:RedisCacheUrl");
            var existingConnection = ConnectionMultiplexer.Connect(redisUrl);

            var resourceLockingService = new ResourceLockingService(existingConnection);

            services.AddSingleton((x) => resourceLockingService);

            return services;
        }
    }
}

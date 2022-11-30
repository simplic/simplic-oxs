using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Reflection;

namespace Simplic.OxS.ResourceLocking
{
    /// <summary>
    /// Adds the possibility to add hook definitions to a service
    /// </summary>
    public static class ResourceLockingExtension
    {
        /// <summary>
        /// Adds the mongodb extension
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="Assemblies">List of assemblies that should be loaded for reading hook definitions</param>
        /// <returns>Service collection instance</returns>
        public static IServiceCollection AddResourceLocking(this IServiceCollection services, IConfiguration configuration, IList<string>? assemblies = null)
        {
            if (assemblies != null)
            {
                foreach (var assemblyName in assemblies)
                {
                    Console.WriteLine($"Load assembly for retreiving hook definitions: {assemblyName}");
                    try
                    {
                        Assembly.Load(assemblyName);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Could not load assembly {assemblyName}", ex);
                    }
                }
            }

            var redisUrl = configuration.GetValue<string>("Redis:RedisCacheUrl");
            var existingConnection = ConnectionMultiplexer.Connect(redisUrl);

            var resourceLockingService = new ResourceLockingService(existingConnection);

            services.AddSingleton((x) => resourceLockingService);

            return services;
        }
    }
}

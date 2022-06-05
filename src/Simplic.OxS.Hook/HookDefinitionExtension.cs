using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Simplic.OxS.Hook
{
    /// <summary>
    /// Adds the possibility to add hook definitions to a service
    /// </summary>
    internal static class HookDefinitionExtension
    {
        /// <summary>
        /// Adds the mongodb extension
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Service configuration</param>
        /// <returns>Service collection instance</returns>
        internal static IServiceCollection AddHookDefinition(this IServiceCollection services, IConfiguration configuration)
        {
            return services;
        }
    }
}

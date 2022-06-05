using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Simplic.OxS.Hook
{
    /// <summary>
    /// Adds the possibility to add hook definitions to a service
    /// </summary>
    public static class HookDefinitionExtension
    {
        /// <summary>
        /// Adds the mongodb extension
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="Assemblies">List of assemblies that should be loaded for reading hook definitions</param>
        /// <returns>Service collection instance</returns>
        public static IServiceCollection AddHookDefinition(this IServiceCollection services, IList<string>? assemblies = null)
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

            var definitionService = new HookDefinitionService();

            foreach (var type in GetHookDefinitionTypes())
            {
                Console.WriteLine($"Add hook definition {type.Value.Name}");
                definitionService.Definitions.Add(type);
            }

            services.AddSingleton<HookDefinitionService>((x) => definitionService);

            return services;
        }

        /// <summary>
        /// Get all <see cref="HookDefinitionAttribute"/>
        /// </summary>
        /// <returns>Dictionary of types (payload) and definition information</returns>
        private static IDictionary<Type, HookDefinitionAttribute> GetHookDefinitionTypes()
        {
            var hooks = new Dictionary<Type, HookDefinitionAttribute>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName != null && x.FullName.ToLower().Contains("oxs")))
            {
                foreach (Type type in assembly.GetTypes())
                {
                    var attribute = type.GetCustomAttributes<HookDefinitionAttribute>(true).FirstOrDefault();

                    if (attribute != null)
                        hooks.Add(type, attribute);
                }
            }

            return hooks;
        }
    }
}

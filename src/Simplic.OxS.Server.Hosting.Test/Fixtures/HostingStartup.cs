using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test.Fixtures
{
    /// <summary>
    /// What every startup of these tests shares: the entity types it names, the controllers it
    /// maps, and a schema posture stated rather than read from the machine.
    /// </summary>
    public abstract class HostingStartup(IConfiguration configuration, IWebHostEnvironment environment) : Bootstrap(configuration, environment)
    {
        /// <summary>The types the schema scans: the startup's own, never another startup's.</summary>
        protected abstract Type[] EntityTypes { get; }

        /// <summary>The controllers the host maps; no other controller of the loaded assemblies is.</summary>
        protected virtual Type[] Controllers => [];

        /// <inheritdoc/>
        protected override IList<Assembly> GetOxQLTypeAssemblies() => [new TypeSet(EntityTypes)];

        /// <inheritdoc/>
        protected override void RegisterServices(IServiceCollection services)
        {
        }

        /// <summary>A build agent's own variables do not decide whether the start is strict.</summary>
        protected override void ConfigureOxSchema(OxSchemaOptionsBuilder schema) => schema.ContinuousIntegration = false;

        /// <inheritdoc/>
        protected override void MvcBuilder(IMvcBuilder builder)
        {
            Type[] mapped = Controllers;

            builder.ConfigureApplicationPartManager(manager =>
            {
                manager.ApplicationParts.Clear();

                foreach (var assembly in mapped.Select(controller => controller.Assembly).Distinct())
                    manager.ApplicationParts.Add(new AssemblyPart(assembly));

                manager.FeatureProviders.Remove(manager.FeatureProviders.OfType<ControllerFeatureProvider>().Single());
                manager.FeatureProviders.Add(new Listed(mapped));
            });
        }

        private sealed class Listed(Type[] controllers) : ControllerFeatureProvider
        {
            protected override bool IsController(TypeInfo typeInfo) => controllers.Contains(typeInfo.AsType());
        }
    }
}

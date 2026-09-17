using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OxQL.Core.Engine;
using OxQL.Core.Models;
using OxQL.Model;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>Registers the schema registry and forces its build while the host starts.</summary>
    public static class OxSchemaServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the registry as a singleton and installs a startup filter that builds it
        /// before the first request, so a fail-fast refusal is a failed start rather than a failed
        /// request and the findings are logged exactly once. The entity model the registry builds
        /// is handed to the query engine as its <see cref="IEntityModelProvider"/>, so the engine
        /// binds against exactly the model the document describes. A second call is a no-op.
        /// </summary>
        public static IServiceCollection AddOxSchema(this IServiceCollection services, Action<OxSchemaOptionsBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var builder = new OxSchemaOptionsBuilder
            {
                ContinuousIntegration = OxSchemaBuildOptions.ReadContinuousIntegration(Environment.GetEnvironmentVariable("CI")),
            };

            configure(builder);

            var options = builder.Build();

            // The query engine's own options, so the document publishes the limits the engine
            // enforces. Resolved when the registry is built, so registration order does not matter.
            services.TryAddSingleton(provider =>
                OxSchemaRegistry.Build(options with { QueryLimits = QueryLimits(provider) ?? options.QueryLimits }));

            // The engine's model is the registry's: built in the startup filter, after every
            // serializer registration, and never a second walk of the same assemblies. Replaces
            // any provider a backend registered as its fallback.
            services.Replace(ServiceDescriptor.Singleton<IEntityModelProvider, OxSchemaEntityModelProvider>());

            services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, OxSchemaStartupFilter>());

            return services;
        }

        private static OxQLOptions? QueryLimits(IServiceProvider provider) =>
            provider.GetService<OxQLOptions>() ?? provider.GetService<IOptions<OxQLOptions>>()?.Value;

        private sealed class OxSchemaStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                app =>
                {
                    OxSchemaStartupLogger.Log(app.ApplicationServices);
                    next(app);
                };
        }

        /// <summary>The engine's model provider over the registry: resolving it builds the registry when nothing else has yet.</summary>
        private sealed class OxSchemaEntityModelProvider(OxSchemaRegistry registry) : IEntityModelProvider
        {
            public EntityModel Model { get; } = registry.Model;
        }
    }
}

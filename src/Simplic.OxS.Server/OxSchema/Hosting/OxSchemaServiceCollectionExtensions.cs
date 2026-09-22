using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OxQL.AspNetCore;
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
        /// binds against exactly the model the document describes, and the engine's own startup
        /// check of the model's remote references fails fast on the schema's
        /// <see cref="OxSchemaBuildOptions.ContinuousIntegration"/> value. A second call is a no-op.
        /// </summary>
        public static IServiceCollection AddOxSchema(this IServiceCollection services, Action<OxSchemaOptionsBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            if (services.Any(descriptor => descriptor.ServiceType == typeof(OxSchemaRegistry)))
                return services;

            var builder = new OxSchemaOptionsBuilder
            {
                ContinuousIntegration = OxSchemaBuildOptions.ReadContinuousIntegration(Environment.GetEnvironmentVariable),
            };

            configure(builder);

            var options = builder.Build();

            // The query engine's own options, so the document publishes the limits the engine
            // enforces. Resolved when the registry is built, so registration order does not matter.
            services.TryAddSingleton(provider => BuildRegistry(provider, options));

            // The engine reads the same variables for its remote reference check; what the host
            // states here wins over that read, so the two strict gates never disagree.
            services.PostConfigure<OxQLEndpointOptions>(endpoint => endpoint.ContinuousIntegration = options.ContinuousIntegration);

            // The engine's model is the registry's: built in the startup filter, after every
            // serializer registration, and never a second walk of the same assemblies. Replaces
            // any provider a backend registered as its fallback.
            services.Replace(ServiceDescriptor.Singleton<IEntityModelProvider, OxSchemaEntityModelProvider>());

            services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, OxSchemaStartupFilter>());

            return services;
        }

        /// <summary>
        /// Builds the registry. Outside fail-fast nothing the build throws leaves this method:
        /// the build runs while the host starts, in every service, over types this package has
        /// never seen, and a metadata defect must not take a service down. The host then serves
        /// a document without types that says so in <c>diagnostics</c>, the query engine binds
        /// against an empty model, and every other route is unaffected. Everything that
        /// resolves the registry - the startup filter here, the engine's model provider - goes
        /// through this one factory.
        /// </summary>
        private static OxSchemaRegistry BuildRegistry(IServiceProvider provider, OxSchemaBuildOptions options)
        {
            try
            {
                return OxSchemaRegistry.Build(options with { QueryLimits = QueryLimits(provider) ?? options.QueryLimits });
            }
            catch (Exception exception) when (!options.FailFast)
            {
                provider.GetService<ILoggerFactory>()?.CreateLogger("Simplic.OxS.Server.OxSchema").LogCritical(
                    exception,
                    "Ox schema build failed for service={Service}: the host serves a schema document without types and the query engine knows no entities until the defect is fixed",
                    options.ServiceName);

                return OxSchemaRegistry.BuildDegraded(options, exception);
            }
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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OxQL.Core.Models;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>Registers the schema registry and forces its build while the host starts.</summary>
    public static class OxSchemaServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the registry as a singleton and installs a startup filter that builds it
        /// before the first request, so a fail-fast refusal is a failed start rather than a failed
        /// request and the findings are logged exactly once. A second call is a no-op.
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
    }
}

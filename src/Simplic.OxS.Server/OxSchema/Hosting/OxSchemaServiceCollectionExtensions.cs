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
        /// <remarks>
        /// A service built on <see cref="Bootstrap"/> does not call this: the host registers the
        /// schema itself, and the place for a service's schema options is
        /// <c>Bootstrap.ConfigureOxSchema</c>. Where a service calls it all the same, the host's
        /// registration stands, with what the call configured merged into it, and the start
        /// logs one warning that says so.
        /// </remarks>
        public static IServiceCollection AddOxSchema(this IServiceCollection services, Action<OxSchemaOptionsBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            if (RegistrationOf(services) is { } existing)
            {
                existing.IgnoredCalls++;

                return services;
            }

            if (services.Any(descriptor => descriptor.ServiceType == typeof(OxSchemaRegistry)))
                return services;

            var registration = Register(services);

            configure(registration.Builder);
            registration.Options = registration.Builder.Build();
            registration.CallsBeforeTheHost++;

            return services;
        }

        /// <summary>
        /// The registration of a host built on <see cref="Bootstrap"/>, which is the one that
        /// stands: <paramref name="configure"/> fills the host's inputs, and
        /// <paramref name="beforeBuild"/> runs right before the registry is built, whoever
        /// resolves the registry first. The build freezes the MongoDB class map of every type it
        /// reaches, so what a host still has to run for its class maps to be registered goes
        /// there. What the step throws is not a schema defect and stops the start in every
        /// environment.
        /// <para>
        /// Where the service called <see cref="AddOxSchema(IServiceCollection, Action{OxSchemaOptionsBuilder})"/>
        /// before, the host's inputs are applied on top of what that call configured: the names,
        /// the api version and the environment are the host's; the type assemblies and the
        /// controllers are the host's followed by the call's; retired ids and reference
        /// declarations add up; and <paramref name="configure"/> decides the rest as far as it
        /// sets it. A registry the service registered by itself is replaced. The start logs
        /// either as one warning (<see cref="OxSchemaRegistration.Warning"/>).
        /// </para>
        /// </summary>
        internal static IServiceCollection AddHostOxSchema(this IServiceCollection services, Action<OxSchemaOptionsBuilder> configure, Action<IServiceProvider>? beforeBuild)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var registration = RegistrationOf(services);

            if (registration is null)
            {
                // A registry that is not this registration's has none of the host's inputs and no step before its build.
                var foreign = services.Where(descriptor => descriptor.ServiceType == typeof(OxSchemaRegistry)).ToList();

                foreach (var descriptor in foreign)
                    services.Remove(descriptor);

                registration = Register(services);
                registration.ReplacedRegistry = foreign.Count > 0;
            }

            var builder = registration.Builder;
            var (assemblies, controllers) = (builder.TypeAssemblies, builder.ControllerTypes);

            configure(builder);

            builder.TypeAssemblies = [.. builder.TypeAssemblies.Concat(assemblies).Distinct()];
            builder.ControllerTypes = [.. builder.ControllerTypes.Concat(controllers).Distinct()];

            registration.BeforeBuild = beforeBuild;
            registration.Options = builder.Build();

            return services;
        }

        /// <summary>The registration these extensions made in <paramref name="services"/>, if any.</summary>
        private static OxSchemaRegistration? RegistrationOf(IServiceCollection services) =>
            services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(OxSchemaRegistration))?.ImplementationInstance as OxSchemaRegistration;

        /// <summary>
        /// Registers the registry, the engine's model provider and the startup filter over a new
        /// <see cref="OxSchemaRegistration"/>. Each reads the registration's options when it is
        /// resolved, so they are the ones of the last call that set them.
        /// </summary>
        private static OxSchemaRegistration Register(IServiceCollection services)
        {
            var registration = new OxSchemaRegistration(new OxSchemaOptionsBuilder
            {
                ContinuousIntegration = OxSchemaBuildOptions.ReadContinuousIntegration(Environment.GetEnvironmentVariable),
            });

            services.AddSingleton(registration);

            // The query engine's own options, so the document publishes the limits the engine
            // enforces. Resolved when the registry is built, so registration order does not matter.
            services.AddSingleton(provider =>
            {
                registration.BeforeBuild?.Invoke(provider);

                return BuildRegistry(provider, registration.Options!);
            });

            // The engine reads the same variables for its remote reference check; what the host
            // states here wins over that read, so the two strict gates never disagree.
            services.PostConfigure<OxQLEndpointOptions>(endpoint => endpoint.ContinuousIntegration = registration.Options!.ContinuousIntegration);

            // The engine's model is the registry's: built in the startup filter, after every
            // serializer registration, and never a second walk of the same assemblies. Replaces
            // any provider a backend registered as its fallback.
            services.Replace(ServiceDescriptor.Singleton<IEntityModelProvider, OxSchemaEntityModelProvider>());

            services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, OxSchemaStartupFilter>());

            return registration;
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

        /// <summary>
        /// The engine's model provider over the registry: resolving it builds the registry when
        /// nothing else has yet. The revision is the published document's, which explain answers
        /// carry as <c>schemaRevision</c>. <see cref="Degraded"/> is always said, never left to the
        /// engine's reading of the entity count: a service without entities is not degraded.
        /// </summary>
        private sealed class OxSchemaEntityModelProvider(OxSchemaRegistry registry) : IEntityModelProvider
        {
            public EntityModel Model { get; } = registry.Model;

            public string? SchemaRevision { get; } = registry.Revision;

            /// <summary>Whether the model is what a failed build left (<see cref="OxSchemaRegistry.Degraded"/>).</summary>
            public bool? Degraded { get; } = registry.Degraded;
        }
    }

    /// <summary>
    /// What <see cref="OxSchemaServiceCollectionExtensions"/> registered in a service collection:
    /// the builder every call fills, the options the registry is built from, and what the calls
    /// were, for the warning of a start whose service registered the schema beside its host.
    /// </summary>
    internal sealed class OxSchemaRegistration(OxSchemaOptionsBuilder builder)
    {
        /// <summary>The inputs, filled by the service's call where there was one and then by the host.</summary>
        public OxSchemaOptionsBuilder Builder { get; } = builder;

        /// <summary>The options of the last call that filled <see cref="Builder"/>.</summary>
        public OxSchemaBuildOptions? Options { get; set; }

        /// <summary>The host's step before the registry is built.</summary>
        public Action<IServiceProvider>? BeforeBuild { get; set; }

        /// <summary>How many calls of the public registration came before the host's.</summary>
        public int CallsBeforeTheHost { get; set; }

        /// <summary>How many calls of the public registration came after a registration and changed nothing.</summary>
        public int IgnoredCalls { get; set; }

        /// <summary>Whether the host replaced a registry the service had registered by itself.</summary>
        public bool ReplacedRegistry { get; set; }

        /// <summary>
        /// What a start says, once, where the service registered the schema beside a host that
        /// registers it; null where it did not. A host has a step before the build, a
        /// registration of the public call alone has none and is not warned about.
        /// </summary>
        public string? Warning()
        {
            const string Place = " The place for a service's schema options is ConfigureOxSchema of its startup: move them there and remove the call.";

            if (BeforeBuild is null)
                return null;

            if (CallsBeforeTheHost > 0)
                return "Ox schema: the service called AddOxSchema before the host registered the schema. The host's registration stands, with the step that runs the mapper profiles before the build: "
                    + "the service name, the api name and version and the environment are the host's, the type assemblies and controllers are the host's and then the call's, "
                    + "and the retired entity ids, reference declarations and other options of the call were kept where the host does not set them."
                    + Place;

            if (ReplacedRegistry)
                return "Ox schema: the service registered an OxSchemaRegistry before the host registered the schema. The host's registration took its place, with the step that runs the mapper profiles before the build."
                    + Place;

            return IgnoredCalls > 0
                ? "Ox schema: the service called AddOxSchema after the host registered the schema. The call changed nothing." + Place
                : null;
        }
    }
}

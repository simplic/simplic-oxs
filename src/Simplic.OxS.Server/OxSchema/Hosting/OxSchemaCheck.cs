using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using OxQL.Core.Models;
using OxQL.Model;
using Simplic.OxS.Server.Extensions;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Builds the Ox schema of a service without starting its host, for a unit test of the
    /// service: the document, every finding and every declared reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The check creates the service's startup and runs what a host runs before it builds the
    /// schema, in the host's order: the guid serializer of the base, <c>RegisterBsonClassMaps</c>,
    /// <c>RegisterServices</c> where asked for, the mapper profiles, then
    /// <c>GetOxQLTypeAssemblies</c>, <c>ConfigureModelDefinitions</c> and
    /// <c>ConfigureOxSchema</c>. It registers no service, opens no connection and reads no
    /// environment variable.
    /// </para>
    /// <para>
    /// MongoDB class maps and serializers are registered per process. The class-map hook and the
    /// mapper profiles of a startup type therefore run once per process, however often the check
    /// is called and whether or not a host of that type was built before; what a service
    /// registers elsewhere has to bear a second call itself.
    /// </para>
    /// <para>
    /// The build never fails fast: a finding that stops a <c>Development</c> host is returned
    /// like every other, with <see cref="OxSchemaFinding.Refuses"/> set. An exception the
    /// service's own code throws, or the build itself, reaches the caller.
    /// </para>
    /// </remarks>
    public static class OxSchemaCheck
    {
        /// <summary>Builds the schema of the service whose startup is <typeparamref name="TStartup"/>.</summary>
        /// <param name="configure">Adjusts the environment, the configuration and whether <c>RegisterServices</c> runs.</param>
        public static OxSchemaCheckResult Build<TStartup>(Action<OxSchemaCheckOptions>? configure = null)
            where TStartup : Bootstrap =>
            Build(typeof(TStartup), configure);

        /// <summary>Builds the schema of the service whose startup is <paramref name="startupType"/>.</summary>
        /// <param name="startupType">A concrete <see cref="Bootstrap"/> subclass with the constructor every startup has: <c>(IConfiguration, IWebHostEnvironment)</c>.</param>
        /// <param name="configure">Adjusts the environment, the configuration and whether <c>RegisterServices</c> runs.</param>
        /// <exception cref="ArgumentException"><paramref name="startupType"/> is no concrete <see cref="Bootstrap"/> subclass, or has no such constructor.</exception>
        public static OxSchemaCheckResult Build(Type startupType, Action<OxSchemaCheckOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(startupType);

            if (startupType.IsAbstract || !typeof(Bootstrap).IsAssignableFrom(startupType))
                throw new ArgumentException($"'{startupType.FullName}' is not a concrete subclass of {nameof(Bootstrap)}.", nameof(startupType));

            var check = new OxSchemaCheckOptions();

            configure?.Invoke(check);

            var configuration = check.Configuration ?? new ConfigurationBuilder().Build();
            var startup = Create(startupType, configuration, new CheckEnvironment(startupType, check.EnvironmentName));

            MongoDbExtension.RegisterGuidSerializer();
            startup.EnsureBsonClassMaps();

            if (check.RunRegisterServices)
                startup.RegisterServicesInto(new ServiceCollection());

            startup.EnsureMapperProfiles();

            var schema = new OxSchemaOptionsBuilder();

            startup.FillOxSchema(schema, startup.OxQLTypeAssemblies());

            // The limits the host's engine would publish, from the same section.
            var limits = new OxQLOptions();

            configuration.GetSection("OxQL").Bind(limits);
            limits.Normalise();

            // Never strict: a check returns the findings a strict host would refuse to start on.
            var registry = OxSchemaRegistry.Build(schema.Build() with { EnvironmentName = "", ContinuousIntegration = false, QueryLimits = limits });

            return new OxSchemaCheckResult(registry, ReferencesOf(registry.Model));
        }

        /// <summary>Creates the startup the way the host builder does, and lets what its constructor throws through as it is.</summary>
        private static Bootstrap Create(Type startupType, IConfiguration configuration, IWebHostEnvironment environment)
        {
            if (startupType.GetConstructor([typeof(IConfiguration), typeof(IWebHostEnvironment)]) is not { } constructor)
                throw new ArgumentException(
                    $"'{startupType.FullName}' has no public constructor (IConfiguration, IWebHostEnvironment), the one a host creates a startup through.",
                    nameof(startupType));

            try
            {
                return (Bootstrap)constructor.Invoke([configuration, environment]);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();

                throw;
            }
        }

        /// <summary>
        /// Every target of every reference case of the model, in entity, path and declaration
        /// order, each (path, target, item, field) once. A dictionary's <c>*</c> path repeats its
        /// member's reference and is not listed again.
        /// </summary>
        private static IReadOnlyList<OxSchemaCheckReference> ReferencesOf(EntityModel model)
        {
            var references = new List<OxSchemaCheckReference>();

            foreach (var entity in model.Entities.Values.OrderBy(entity => entity.Id, StringComparer.Ordinal))
                foreach (var path in entity.Paths)
                {
                    if (!ReferenceEquals(path.Shape, path.Member))
                        continue;

                    foreach (var reference in path.Member.References)
                        foreach (var target in reference.Targets)
                        {
                            var listed = new OxSchemaCheckReference(entity.Id, path.Wire, target.Entity, target.Item, target.Field, target.IsRemote);

                            if (!references.Contains(listed))
                                references.Add(listed);
                        }
                }

            return references;
        }

        /// <summary>The environment a startup is created with: a name, and no files.</summary>
        private sealed class CheckEnvironment(Type startupType, string environmentName) : IWebHostEnvironment
        {
            public string EnvironmentName { get; set; } = environmentName;

            public string ApplicationName { get; set; } = startupType.Assembly.GetName().Name ?? "";

            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

            public string WebRootPath { get; set; } = AppContext.BaseDirectory;

            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        }
    }

    /// <summary>What <see cref="OxSchemaCheck"/> creates the startup with.</summary>
    public sealed class OxSchemaCheckOptions
    {
        /// <summary>
        /// The environment name the startup sees. <c>Production</c> by default. It never makes
        /// the check fail fast.
        /// </summary>
        public string EnvironmentName { get; set; } = "Production";

        /// <summary>
        /// The configuration the startup is created with, and the <c>OxQL</c> section the
        /// published limits are read from. Empty by default, which publishes the engine's
        /// default limits; with the service's own configuration the document's revision is the
        /// one its host publishes.
        /// </summary>
        public IConfiguration? Configuration { get; set; }

        /// <summary>
        /// Whether the startup's <c>RegisterServices</c> runs, into a service collection that is
        /// thrown away. Off by default, because a service may read configuration there that a
        /// test does not have. Turn it on for a service that registers class maps or serializers
        /// in <c>RegisterServices</c>; such a registration has to bear a second call in one
        /// process.
        /// </summary>
        public bool RunRegisterServices { get; set; }
    }

    /// <summary>What <see cref="OxSchemaCheck"/> built.</summary>
    public sealed class OxSchemaCheckResult
    {
        internal OxSchemaCheckResult(OxSchemaRegistry registry, IReadOnlyList<OxSchemaCheckReference> references)
        {
            Registry = registry;
            References = references;
        }

        /// <summary>Everything the host would serve: the entity model, the document, its body and revision, the findings.</summary>
        public OxSchemaRegistry Registry { get; }

        /// <summary>The schema document, as <c>GET /schema</c> answers it.</summary>
        public OxSchemaDocument Document => Registry.Document;

        /// <summary>The body of <c>GET /schema</c> as text.</summary>
        public string Json => Encoding.UTF8.GetString(Registry.Body);

        /// <summary>
        /// Every finding of the build, the ones the startup log writes: refusing first, then
        /// published, then log-only, each group by code and target.
        /// </summary>
        public IReadOnlyList<OxSchemaFinding> Findings => Registry.Findings;

        /// <summary>Every declared reference the build kept: a declaration that is missing here was dropped, and a finding says why.</summary>
        public IReadOnlyList<OxSchemaCheckReference> References { get; }
    }

    /// <summary>One target of a declared reference, as the model holds it.</summary>
    /// <param name="Entity">The entity the reference is declared in.</param>
    /// <param name="Path">The wire path of the member that carries it, e.g. <c>resources.vehicleId</c>.</param>
    /// <param name="Target">The target entity id.</param>
    /// <param name="Item">The path of the target's item collection whose element the value names; null when it names the entity.</param>
    /// <param name="Field">The path of the target the value matches.</param>
    /// <param name="Remote">Whether another service owns the target: the service the id's prefix names.</param>
    public sealed record OxSchemaCheckReference(string Entity, string Path, string Target, string? Item, string Field, bool Remote)
    {
        /// <summary>
        /// The service that owns the target, by the prefix of its id. For a remote target the
        /// calling service needs <c>InternalHosts:{Service}</c> and, where that owner does not
        /// answer on <c>v1</c>, <c>InternalApiVersions:{Service}</c>.
        /// </summary>
        public string Service => Target.Split('.')[0];
    }
}

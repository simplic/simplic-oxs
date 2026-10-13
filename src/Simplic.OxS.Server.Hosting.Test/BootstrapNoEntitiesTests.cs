using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// A service that names an empty list of entity assemblies declares no entities, and is not
    /// told that it forgot them; a service that names no list at all is.
    /// </summary>
    public sealed class BootstrapNoEntitiesTests
    {
        private const string Declared = "Ox schema: the service declares no OxQL entities: GetOxQLTypeAssemblies names no assembly.";

        [Fact]
        public async Task AnEmptyListOfEntityAssemblies_IsAServiceWithoutEntities_SaidOnceAsInformation()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<NoEntitiesStartup>(log);

            var registry = host.Services.GetRequiredService<OxSchemaRegistry>();

            registry.Findings.Should().BeEmpty();
            registry.Document.Diagnostics.Should().BeNull();
            registry.Document.Types.Should().BeEmpty();
            registry.Degraded.Should().BeFalse();

            log.Entries.Where(entry => entry.Message.StartsWith("Ox schema", StringComparison.Ordinal) && !entry.Message.StartsWith("Ox schema built", StringComparison.Ordinal))
                .Should().Equal((LogLevel.Information, Declared));
            log.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning && entry.Message.Contains("schema", StringComparison.OrdinalIgnoreCase));

            // The schema test of such a service builds the same document.
            OxSchemaCheck.Build<NoEntitiesStartup>().Findings.Should().BeEmpty();
        }

        [Fact]
        public async Task NoListOfEntityAssemblies_IsNamedAsBefore()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<NoListStartup>(log);

            var registry = host.Services.GetRequiredService<OxSchemaRegistry>();

            registry.Findings.Should().ContainSingle().Which.Code.Should().Be(OxSchemaCodes.EntityAssembliesMissing);
            registry.Document.Diagnostics.Should().ContainSingle().Which.Code.Should().Be("entity-assemblies-missing");
            log.Entries.Should().ContainSingle(entry => entry.Message.StartsWith("Ox schema finding: entity-assemblies-missing", StringComparison.Ordinal))
                .Which.Level.Should().Be(LogLevel.Warning);
            log.Entries.Should().NotContain(entry => entry.Message == Declared);
        }
    }

    /// <summary>A service that has no entities and says so.</summary>
    public class NoEntitiesStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Noentities";

        protected override Type[] EntityTypes => [];

        protected override IList<Assembly> GetOxQLTypeAssemblies() => [];
    }

    /// <summary>A service that names no list of entity assemblies.</summary>
    public class NoListStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Nolist";

        protected override Type[] EntityTypes => [];

        protected override IList<Assembly> GetOxQLTypeAssemblies() => null!;
    }
}

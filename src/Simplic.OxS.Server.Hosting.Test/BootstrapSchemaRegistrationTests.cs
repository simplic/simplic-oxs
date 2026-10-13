using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// The schema of a host built through <see cref="Bootstrap"/> is the one the host registers,
    /// also where the service registers one itself: with the host's inputs, with the step that
    /// runs the mapper profiles before the build, and with a warning that names the place for a
    /// service's schema options.
    /// </summary>
    public sealed class BootstrapSchemaRegistrationTests
    {
        private const string Place = " The place for a service's schema options is ConfigureOxSchema of its startup: move them there and remove the call.";

        private static List<(LogLevel Level, string Message)> SaidOfTheRegistration(CapturedLog log) =>
            [.. log.Entries.Where(entry => entry.Message.StartsWith("Ox schema: the service ", StringComparison.Ordinal))];

        [Fact]
        public async Task ASchemaTheServiceRegisteredBeforeTheHost_IsMergedIntoTheHosts_WhichRunsTheMapperProfilesBeforeTheBuild()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<QuayStartup>(log);

            var registry = host.Services.GetRequiredService<OxSchemaRegistry>();
            var document = registry.Document;

            // The host's names, not the call's.
            document.Service.Should().Be("quay");
            document.Api.Name.Should().Be("quay-api");
            document.Api.Version.Should().Be("v1");

            // The entities of the host's assemblies and of the call's.
            document.Types.Keys.Should().Contain(["quay.berth", "quay.mooring"]);

            // The class maps of the mapper profiles were registered before the build.
            QuayStartup.ProfileRuns.Should().Be(1);
            (document.Types["t_berthGear"].Variants ?? []).Select(variant => variant.Name).Should().BeEquivalentTo(["CraneGear", "RampGear"]);
            registry.Findings.Should().NotContain(finding => finding.Code == global::OxQL.Model.BuildCodes.PolymorphicSubtypeUnregistered);

            // What only the call configured is kept.
            document.Types["quay.berth"].Aliases.Should().Contain("quay.dock");
            registry.RequireAuthorization.Should().BeTrue();

            SaidOfTheRegistration(log).Should().Equal((
                LogLevel.Warning,
                "Ox schema: the service called AddOxSchema before the host registered the schema. The host's registration stands, with the step that runs the mapper profiles before the build: "
                + "the service name, the api name and version and the environment are the host's, the type assemblies and controllers are the host's and then the call's, "
                + "and the retired entity ids, reference declarations and other options of the call were kept where the host does not set them."
                + Place));
        }

        [Fact]
        public async Task ASchemaTheServiceRegistersAfterTheHost_ChangesNothing_AndIsNamed()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<PierStartup>(log);

            var document = host.Services.GetRequiredService<OxSchemaRegistry>().Document;

            document.Service.Should().Be("pier");
            document.Api.Version.Should().Be("v1");
            document.Types.Keys.Should().Contain("pier.bollard");

            SaidOfTheRegistration(log).Should().Equal((LogLevel.Warning, "Ox schema: the service called AddOxSchema after the host registered the schema. The call changed nothing." + Place));
        }

        [Fact]
        public async Task ARegistryTheServiceRegisteredItself_IsReplacedByTheHosts_AndNamed()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<JettyStartup>(log);

            host.Services.GetServices<OxSchemaRegistry>().Should().ContainSingle();

            var document = host.Services.GetRequiredService<OxSchemaRegistry>().Document;

            document.Service.Should().Be("jetty");
            document.Types.Keys.Should().Contain("jetty.fender");

            SaidOfTheRegistration(log).Should().Equal((
                LogLevel.Warning,
                "Ox schema: the service registered an OxSchemaRegistry before the host registered the schema. The host's registration took its place, with the step that runs the mapper profiles before the build."
                + Place));
        }

        [Fact]
        public async Task AServiceThatLeavesTheSchemaToItsHost_IsToldNothing()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<ParcelStartup>(log);

            SaidOfTheRegistration(log).Should().BeEmpty();
            log.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning && entry.Message.StartsWith("Ox schema", StringComparison.Ordinal));
        }
    }
}

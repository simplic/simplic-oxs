using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// A host built through <see cref="Bootstrap"/>: the service's MongoDB class maps are in place
    /// before the schema is built, wherever the service registers them.
    /// </summary>
    public sealed class BootstrapClassMapTests
    {
        private static IReadOnlyList<string> VariantsOf(IHost host, string structuralId) =>
            [.. (host.Services.GetRequiredService<OxSchemaRegistry>().Document.Types[structuralId].Variants ?? []).Select(variant => variant.Name)];

        private static IEnumerable<OxSchemaFinding> UnregisteredSubtypes(IHost host) =>
            host.Services.GetRequiredService<OxSchemaRegistry>().Findings.Where(finding => finding.Code == global::OxQL.Model.BuildCodes.PolymorphicSubtypeUnregistered);

        [Fact]
        public async Task ClassMapsRegisteredInTheMapperProfiles_AreInTheSchemaAndTheMapperWorks()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<LedgerStartup>(log);
            using var client = host.GetTestClient();

            VariantsOf(host, "t_bookingItem").Should().BeEquivalentTo(["ArticleBookingItem", "NoteBookingItem"]);
            VariantsOf(host, "t_iBookingStep").Should().BeEquivalentTo(["ApproveStep", "RejectStep"]);
            UnregisteredSubtypes(host).Should().BeEmpty();

            // The mapper exists once the host is up, and every request gets that one.
            LedgerStartup.ProfileRuns.Should().Be(1);

            foreach (var attempt in Enumerable.Range(0, 2))
            {
                using var answer = await client.GetAsync("/ledger-api/v1/booking-probe");

                answer.StatusCode.Should().Be(HttpStatusCode.OK, $"request {attempt} resolves the mapper");
                (await answer.Content.ReadAsStringAsync()).Should().Contain("B-1");
            }

            LedgerStartup.ProfileRuns.Should().Be(1, "the profiles and the registrations in them run once per host");
            log.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Error);
        }

        [Fact]
        public async Task TheClassMapHook_RunsOncePerProcessAndBeforeTheSchemaIsBuilt()
        {
            using (var first = await BootstrapHost.StartAsync<DepotStartup>())
            {
                VariantsOf(first, "t_yardSlot").Should().BeEquivalentTo(["CoveredSlot", "OpenSlot"]);
                UnregisteredSubtypes(first).Should().BeEmpty();
            }

            // A second host of the same process: the unguarded registrations would throw.
            using var second = await BootstrapHost.StartAsync<DepotStartup>();

            VariantsOf(second, "t_yardSlot").Should().BeEquivalentTo(["CoveredSlot", "OpenSlot"]);
            DepotStartup.HookRuns.Should().Be(1);
        }

        [Fact]
        public async Task ARegistrationCalledFromConfigureOxSchema_KeepsWorking()
        {
            using var host = await BootstrapHost.StartAsync<FleetStartup>();

            VariantsOf(host, "t_unitBody").Should().BeEquivalentTo(["BoxBody", "TankBody"]);
            UnregisteredSubtypes(host).Should().BeEmpty();
            FleetClassMaps.Runs.Should().Be(1);
        }

        [Fact]
        public async Task TheHooksOfAStartup_RunInTheOrderTheUpgradeNotesName()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<OrderedStartup>(log);

            // The class map hook first; the mapper profiles once the service's hosted services have started, and before the schema is built.
            OrderedStartup.Steps.Should().Equal("RegisterBsonClassMaps", "RegisterServices", "ConfigureOxSchema", "a hosted service starts", "RegisterMapperProfiles", "the schema is built");
        }

        [Fact]
        public async Task FaultyMapperProfiles_StopTheStart()
        {
            using var host = BootstrapHost.Build<FaultyStartup>();

            var start = () => host.StartAsync();

            (await start.Should().ThrowAsync<InvalidOperationException>()).WithMessage(FaultyStartup.Fault);
        }
    }
}

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// The schema check a service calls from a unit test: what it builds without a host, and that
    /// it runs the service's own code the way a host does.
    /// </summary>
    public sealed class OxSchemaCheckTests
    {
        private const string SubtypeUnregistered = global::OxQL.Model.BuildCodes.PolymorphicSubtypeUnregistered;

        [Fact]
        public void Build_AnswersTheDocumentTheFindingsAndTheReferences()
        {
            var result = OxSchemaCheck.Build<CatalogStartup>();

            result.Document.Service.Should().Be("catalog");
            result.Document.Api.Name.Should().Be("catalog-api");
            result.Document.Types.Keys.Should().Contain(["catalog.product", "catalog.brand", "t_productPart"]);
            result.Registry.Revision.Should().StartWith("sha256:");

            using var json = JsonDocument.Parse(result.Json);

            json.RootElement.GetProperty("revision").GetString().Should().Be(result.Registry.Revision);

            // The variants the hook and the mapper profiles registered; the forgotten one is a finding.
            (result.Document.Types["t_productPart"].Variants ?? []).Select(variant => variant.Name).Should().BeEquivalentTo(["BoltPart", "ScrewPart"]);
            result.Findings.Should().ContainSingle(finding => finding.Code == SubtypeUnregistered)
                .Which.ClrDetail.Should().Be(typeof(GluePart).FullName);

            // The line a service writes: no reference of ours was dropped. Here one was.
            result.Findings.Where(finding => finding.Code.StartsWith("reference-", StringComparison.Ordinal))
                .Should().ContainSingle().Which.Code.Should().Be("reference-target-field-unknown");

            result.References.Should().Equal(
                new OxSchemaCheckReference("catalog.product", "brandId", "catalog.brand", null, "id", false),
                new OxSchemaCheckReference("catalog.product", "binId", "stock.bin", null, "id", true));
            result.References.Where(reference => reference.Remote).Select(reference => reference.Service).Should().Equal("stock");
        }

        [Fact]
        public void Build_RunsTheHookAndTheMapperProfilesOncePerProcess_AndLeavesRegisterServicesAlone()
        {
            var first = OxSchemaCheck.Build<CatalogStartup>();
            var second = OxSchemaCheck.Build(typeof(CatalogStartup));

            second.Registry.Revision.Should().Be(first.Registry.Revision);
            CatalogStartup.HookRuns.Should().Be(1);
            CatalogStartup.ProfileRuns.Should().Be(1, "the registrations beside the profiles are the process's and bear no second run");
            CatalogStartup.ServiceRuns.Should().Be(0);
        }

        [Fact]
        public void Build_WithRegisterServices_RunsItIntoACollectionOfItsOwn()
        {
            var result = OxSchemaCheck.Build<PressStartup>(check => check.RunRegisterServices = true);

            PressStartup.ServiceRuns.Should().Be(1);
            (result.Document.Types["t_sheetCoat"].Variants ?? []).Select(variant => variant.Name).Should().Equal("MattCoat");
            result.Findings.Should().BeEmpty();
        }

        [Fact]
        public async Task Build_OfAStartupAHostWasBuiltFrom_OrBefore_RunsTheHookOnce()
        {
            var before = OxSchemaCheck.Build<DepotStartup>();

            using (await BootstrapHost.StartAsync<DepotStartup>())
            {
            }

            var after = OxSchemaCheck.Build<DepotStartup>();

            DepotStartup.HookRuns.Should().Be(1);
            (after.Document.Types["t_yardSlot"].Variants ?? []).Select(variant => variant.Name).Should().BeEquivalentTo(["CoveredSlot", "OpenSlot"]);
            after.Registry.Revision.Should().Be(before.Registry.Revision);
        }

        [Theory]
        [InlineData("Development")]
        [InlineData("Local")]
        [InlineData("Production")]
        public void Build_NeverFailsFast_AFindingThatStopsAStrictHostIsReturned(string environment)
        {
            var result = OxSchemaCheck.Build<MintStartup>(check => check.EnvironmentName = environment);

            MintStartup.SeenEnvironment.Should().Be(environment);
            result.Findings.Should().ContainSingle(finding => finding.Refuses)
                .Which.Should().Match<OxSchemaFinding>(finding => finding.Code == OxSchemaCodes.DuplicateEntityId && finding.Target == "mint.coin");
        }

        [Fact]
        public void Build_HandsTheStartupTheConfiguration_AndPublishesTheLimitsOfItsOxQLSection()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mint:Setting"] = "struck",
                ["OxQL:Limits:MaxOffset"] = "1234",
            }).Build();

            var configured = OxSchemaCheck.Build<MintStartup>(check => check.Configuration = configuration);

            MintStartup.SeenSetting.Should().Be("struck");
            configured.Document.Limits.MaxOffset.Should().Be(1234);

            var plain = OxSchemaCheck.Build<MintStartup>();

            MintStartup.SeenSetting.Should().BeNull();
            plain.Document.Limits.MaxOffset.Should().Be(new global::OxQL.Core.Models.OxQLOptions().Limits.MaxOffset);
            plain.Registry.Revision.Should().NotBe(configured.Registry.Revision, "the limits are inside the revision");
        }

        [Fact]
        public void Build_WhenTheHookThrows_ThrowsThatOnEveryCall_AndRunsTheHookOnce()
        {
            var build = () => OxSchemaCheck.Build<BrokenHookStartup>();

            build.Should().Throw<InvalidOperationException>().WithMessage(BrokenHookStartup.Fault);
            build.Should().Throw<InvalidOperationException>().WithMessage(BrokenHookStartup.Fault);
            BrokenHookStartup.HookRuns.Should().Be(1);
        }

        [Fact]
        public void Build_WhenTheMapperProfilesThrow_ThrowsThat()
        {
            var build = () => OxSchemaCheck.Build<FaultyStartup>();

            build.Should().Throw<InvalidOperationException>().WithMessage(FaultyStartup.Fault);
        }

        [Fact]
        public void Build_WhenTheMapperProfilesThrow_ThrowsThatOnEveryCall_AndRunsTheProfilesOnce()
        {
            var build = () => OxSchemaCheck.Build<BrokenProfilesStartup>();

            var first = build.Should().Throw<InvalidOperationException>().WithMessage(BrokenProfilesStartup.Fault).Which;
            var second = build.Should().Throw<InvalidOperationException>().WithMessage(BrokenProfilesStartup.Fault).Which;

            second.Should().BeSameAs(first, "a later build reports what the profiles threw when they ran");
            BrokenProfilesStartup.ProfileRuns.Should().Be(1);
        }

        [Fact]
        public async Task Build_BesideAHostOfTheSameStartupThatIsStarting_DoesNotRunTheProfilesWhileTheHostRunsThem()
        {
            var check = Task.Run(() => OxSchemaCheck.Build<GatedProfilesStartup>());

            GatedProfilesStartup.Entered.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue("the check runs the profiles");

            // The host needs a mapper of its own and so runs the profiles too, after the check has left them.
            var start = Task.Run(() => BootstrapHost.StartAsync<GatedProfilesStartup>());

            GatedProfilesStartup.SecondEntered.IsSet.Should().BeFalse("the host waits for the run that is inside the profiles");
            start.IsCompleted.Should().BeFalse("the host waits for the run that is inside the profiles");
            GatedProfilesStartup.ProfileRuns.Should().Be(1);

            GatedProfilesStartup.Gate.Set();

            await check;

            using var host = await start;

            GatedProfilesStartup.SecondEntered.IsSet.Should().BeTrue("the host ran the profiles once the check had left them");
            GatedProfilesStartup.ProfileRuns.Should().Be(2);
            GatedProfilesStartup.MostAtOnce.Should().Be(1);
        }

        [Fact]
        public void Build_OfATypeThatIsNoStartup_IsACallerError()
        {
            var notAStartup = () => OxSchemaCheck.Build(typeof(string));
            var abstractStartup = () => OxSchemaCheck.Build(typeof(HostingStartup));
            var oddConstructor = () => OxSchemaCheck.Build<OddStartup>();

            notAStartup.Should().Throw<ArgumentException>().WithMessage("*not a concrete subclass of Bootstrap*");
            abstractStartup.Should().Throw<ArgumentException>().WithMessage("*not a concrete subclass of Bootstrap*");
            oddConstructor.Should().Throw<ArgumentException>().WithMessage("*no public constructor (IConfiguration, IWebHostEnvironment)*");
        }
    }
}

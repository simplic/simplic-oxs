using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// What a host says where a service registers a MongoDB serializer after something looked
    /// that serializer up, and that the order the message names works.
    /// </summary>
    public sealed class BootstrapSerializerOrderTests
    {
        /// <summary>What every explained refusal says after the driver's sentence and the place.</summary>
        private const string Remedy =
            " A registration that ran earlier looked this serializer up, so the driver had already created its default for the type, and a serializer is only registered before that."
            + " Register this serializer at the top of RegisterBsonClassMaps(), before anything that constructs a serializer or a class map."
            + " BsonSerializer.TryRegisterSerializer does not help: it leaves the default in place, and the registration that looked it up keeps it.";

        /// <summary>The driver's own sentence for the value type of a startup.</summary>
        private static string Driver<TMark>() => $"There is already a serializer registered for type {BsonUtils.GetFriendlyTypeName(typeof(Gauge<TMark>))}.";

        private static void IsExplained<TMark>(BsonSerializationException thrown, string place)
        {
            thrown.Message.Should().Be($"{Driver<TMark>()} Thrown in {place}.{Remedy}");

            // What the driver threw is kept, wherever in the chain of causes it was.
            Exception? cause = thrown.InnerException;

            while (cause is not null and not BsonSerializationException)
                cause = cause.InnerException;

            cause.Should().BeOfType<BsonSerializationException>().Which.Message.Should().Be(Driver<TMark>());
        }

        [Fact]
        public void ASerializerRegisteredInRegisterServices_AfterAClassMapOfTheHookLookedItUp_IsRefusedWithTheRemedy()
        {
            var build = () => BootstrapHost.Build<LateInServicesStartup>();

            IsExplained<MarkServices>(build.Should().Throw<BsonSerializationException>().Which, "RegisterServices");
        }

        [Fact]
        public void ASerializerRegisteredInRegisterServices_AfterASerializerOfTheHookKeptIt_IsRefusedWithTheRemedy()
        {
            var build = () => BootstrapHost.Build<LateAfterKeeperStartup>();

            IsExplained<MarkKeeper>(build.Should().Throw<BsonSerializationException>().Which, "RegisterServices");
        }

        [Fact]
        public async Task ASerializerRegisteredInTheMapperProfiles_IsRefusedWithTheRemedy_AlsoAsTheCauseOfWhatTheProfilesThrow()
        {
            using var host = BootstrapHost.Build<LateInProfilesStartup>();

            var start = () => host.StartAsync();

            IsExplained<MarkProfiles>((await start.Should().ThrowAsync<BsonSerializationException>()).Which, "RegisterMapperProfiles");
        }

        [Fact]
        public void ASerializerRegisteredInConfigureOxSchema_IsRefusedWithTheRemedy()
        {
            var build = () => BootstrapHost.Build<LateInSchemaStartup>();

            IsExplained<MarkSchema>(build.Should().Throw<BsonSerializationException>().Which, "ConfigureOxSchema");
        }

        [Fact]
        public void APlainSerializerRegisteredInTheHook_AfterOneThatKeepsIt_IsRefusedWithTheRemedy_OnEveryBuild()
        {
            var build = () => BootstrapHost.Build<WrongOrderInHookStartup>();

            var first = build.Should().Throw<BsonSerializationException>().Which;
            var second = build.Should().Throw<BsonSerializationException>().Which;

            IsExplained<MarkHook>(first, "RegisterBsonClassMaps");
            second.Message.Should().Be(first.Message, "a later host reports what the hook threw when it ran");
            WrongOrderInHookStartup.HookRuns.Should().Be(1);
        }

        [Fact]
        public async Task TheOrderTheRemedyNames_Starts_AndTheSerializerThatKeepsAnotherKeepsTheServicesOwn()
        {
            using var host = await BootstrapHost.StartAsync<OrderedHookStartup>();

            BsonSerializer.LookupSerializer<Gauge<MarkOrdered>>().Should().BeOfType<GaugeSerializer<MarkOrdered>>();
            BsonSerializer.LookupSerializer<List<Gauge<MarkOrdered>>>().Should().BeOfType<ReadingListSerializer<MarkOrdered>>()
                .Which.Kept.Should().BeOfType<GaugeSerializer<MarkOrdered>>();
        }

        [Fact]
        public async Task AClassMapThatLooksNoSerializerUp_LeavesTheSerializerOfItsMemberToBeRegisteredLater()
        {
            using var host = await BootstrapHost.StartAsync<UnfrozenClassMapStartup>();

            BsonSerializer.LookupSerializer<Gauge<MarkUnfrozen>>().Should().BeOfType<GaugeSerializer<MarkUnfrozen>>();
        }

        [Fact]
        public void TheSchemaCheck_ExplainsTheRefusalLikeAHost_AndThrowsItOnEveryCallWithoutASecondRun()
        {
            var check = () => OxSchemaCheck.Build<LateInServicesCheckedStartup>(options => options.RunRegisterServices = true);

            var first = check.Should().Throw<BsonSerializationException>().Which;
            var second = check.Should().Throw<BsonSerializationException>().Which;

            IsExplained<MarkCheckLate>(first, "RegisterServices");
            second.Should().BeSameAs(first);
            LateInServicesCheckedStartup.ServiceRuns.Should().Be(1);
        }

        [Fact]
        public void TheSchemaCheck_RunsRegisterServicesOncePerProcess_SoASerializerRegisteredThereWithoutAGuardBearsASecondCheck()
        {
            var first = OxSchemaCheck.Build<UnguardedInServicesStartup>(options => options.RunRegisterServices = true);
            var second = OxSchemaCheck.Build<UnguardedInServicesStartup>(options => options.RunRegisterServices = true);
            var without = OxSchemaCheck.Build<UnguardedInServicesStartup>();

            UnguardedInServicesStartup.ServiceRuns.Should().Be(1);
            second.Registry.Revision.Should().Be(first.Registry.Revision);
            without.Registry.Revision.Should().Be(first.Registry.Revision);
            BsonSerializer.LookupSerializer<Gauge<MarkCheck>>().Should().BeOfType<GaugeSerializer<MarkCheck>>();
        }

        [Fact]
        public async Task TheSchemaCheck_OfAStartupAHostWasBuiltFrom_DoesNotRunRegisterServicesAgain()
        {
            using (await BootstrapHost.StartAsync<HostThenCheckStartup>())
            {
            }

            OxSchemaCheck.Build<HostThenCheckStartup>(options => options.RunRegisterServices = true);

            HostThenCheckStartup.ServiceRuns.Should().Be(1, "the host registered what the check needs of it");
        }
    }
}

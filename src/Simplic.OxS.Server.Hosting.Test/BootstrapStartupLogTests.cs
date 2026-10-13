using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;
using Simplic.OxS.Server.Hosting.Test.Fixtures;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>What the lines of a start say of the schema, in the number they speak of.</summary>
    public sealed class BootstrapStartupLogTests
    {
        private static async Task<List<(LogLevel Level, string Message)>> LinesAsync<TStartup>()
            where TStartup : Bootstrap
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<TStartup>(log);

            return [.. log.Entries.Where(entry => entry.Message.StartsWith("Ox schema", StringComparison.Ordinal))];
        }

        [Fact]
        public async Task TheSummaryLine_NamesTheEntitiesBesideTheTypes()
        {
            // One entity, and beside it the type its collection holds.
            (await LinesAsync<LoneStartup>()).Should().ContainSingle(line => line.Message.StartsWith("Ox schema built: ", StringComparison.Ordinal))
                .Which.Message.Should().StartWith("Ox schema built: service=lone api=lone-api/v1 entities=1 types=2 revision=sha256:");

            (await LinesAsync<DispatchStartup>()).Should().ContainSingle(line => line.Message.StartsWith("Ox schema built: ", StringComparison.Ordinal))
                .Which.Message.Should().StartWith("Ox schema built: service=dispatch api=dispatch-api/v1 entities=1 types=1 revision=sha256:");
        }

        [Fact]
        public async Task TheLinesThatSayWhatToDo_SpeakOfOneWhereThereIsOne()
        {
            var lines = await LinesAsync<LoneStartup>();

            lines.Should().Contain((
                LogLevel.Error,
                $"Ox schema: 1 stored subtype has no MongoDB class map and is missing from the schema ({typeof(ShelfPart).FullName}). "
                + "Register its class map in Bootstrap.RegisterBsonClassMaps, or list it in [BsonKnownTypes] on the base type: "
                + "a class map registered after the schema is built throws or is not applied."));

            lines.Should().Contain((
                LogLevel.Error,
                "Ox schema: 1 entity id is not <service>.<entity> with this service's name as the prefix (lone_rack), so another service cannot reference it. "
                + "Rename the id in its [OxQLType] and paste the RetireEntityId line of its finding into ConfigureOxSchema: the old id keeps resolving. "
                + "The host starts as before."));
        }

        [Fact]
        public async Task TheLinesThatSayWhatToDo_SpeakOfSeveralWhereThereAreSeveral()
        {
            (await LinesAsync<RegisterStartup>()).Should().ContainSingle(line => line.Message.StartsWith("Ox schema: 2 ", StringComparison.Ordinal))
                .Which.Message.Should().StartWith("Ox schema: 2 entity ids are not <service>.<entity> with this service's name as the prefix (")
                .And.EndWith("so another service cannot reference them. Rename each id in its [OxQLType] and paste the RetireEntityId line of its finding into ConfigureOxSchema: "
                    + "the old id keeps resolving. The host starts as before.");
        }
    }

    /// <summary>An entity whose id carries no service, with a collection of a type that has one stored subclass.</summary>
    [OxQLType("lone_rack", "lone_rack")]
    public class Rack : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public IList<RackPart> Parts { get; set; } = [];
    }

    /// <summary>The base of the stored parts.</summary>
    public abstract class RackPart
    {
        public Guid Id { get; set; }
    }

    /// <summary>A stored part nothing registers a class map for.</summary>
    public class ShelfPart : RackPart
    {
        public int Depth { get; set; }
    }

    /// <summary>A service with one finding of each kind a start says what to do about.</summary>
    public class LoneStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Lone";

        protected override Type[] EntityTypes => [typeof(Rack), typeof(RackPart), typeof(ShelfPart)];
    }
}

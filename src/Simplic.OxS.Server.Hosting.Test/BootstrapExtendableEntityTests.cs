using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Controllers;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// An entity that is declared extendable and has no <c>addon</c> member: the start says so,
    /// and the schema document, the addon definitions of the schema and the addon definition
    /// routes say the same of it.
    /// </summary>
    public sealed class BootstrapExtendableEntityTests
    {
        private const string Finding =
            "Ox schema finding: extendable-without-addon cellar.keg - The entity is declared extendable and has no 'addon' member, so no addon definition can be stored for it "
            + "and GET /schema/addons does not list it. Add the 'addon' member to the entity, or drop the extendable flag. (refuses=False published=False)";

        [Theory]
        [InlineData("Production")]
        [InlineData("Development")]
        public async Task TheStart_NamesTheEntityAndWhatToDoAsAWarning_AndStartsInEveryEnvironment(string environment)
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<CellarStartup>(log, environment);

            log.Entries.Where(entry => entry.Message.Contains("extendable-without-addon", StringComparison.Ordinal))
                .Should().Equal((LogLevel.Warning, Finding + $" [{typeof(Keg).FullName}]"));

            var registry = host.Services.GetRequiredService<OxSchemaRegistry>();
            var finding = registry.Findings.Should().ContainSingle().Subject;

            finding.Code.Should().Be(OxSchemaCodes.ExtendableWithoutAddon).And.Be("extendable-without-addon");
            finding.Target.Should().Be("cellar.keg");
            finding.Refuses.Should().BeFalse();
            finding.Published.Should().BeFalse();

            // The document is the one it was: the flag as declared, and nothing in diagnostics.
            registry.Document.Diagnostics.Should().BeNull();
            registry.Document.Types["cellar.keg"].Extendable.Should().BeTrue();
            registry.Document.Types["cellar.vat"].Extendable.Should().BeTrue();
        }

        [Fact]
        public async Task TheAddonDefinitionsOfTheSchema_ListTheEntitiesThatCanTakeDefinitions_AsTheRouteSaysOfItself()
        {
            using var host = await BootstrapHost.StartAsync<CellarStartup>(new AnsweringEngine().Register);
            using var client = host.GetTestClient();
            using var message = new HttpRequestMessage(HttpMethod.Get, "/cellar-api/v1/schema/addons");

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Callers.Bearer());

            using var answer = await client.SendAsync(message);

            answer.StatusCode.Should().Be(HttpStatusCode.OK);
            OpenApiDocumentTests.Keys(JsonNode.Parse(await answer.Content.ReadAsStringAsync())).Should().Equal("cellar.vat");

            var route = (await OpenApiDocumentTests.DocumentAsync(host, "cellar-api"))["paths"]!["/schema/addons"]!["get"]!;

            route["summary"]!.GetValue<string>().Should().Be("The addon definitions of the caller's organisation, per entity that takes them.");
            route["description"]!.GetValue<string>().Should().StartWith(
                "Answers one list per entity of this service that takes addon definitions, keyed by entity id: an entity that is declared extendable and has an `addon` member. ");
        }

        [Fact]
        public async Task ADefinitionForTheEntity_IsRefusedWithTheSameReason()
        {
            using var host = await BootstrapHost.StartAsync<CellarStartup>(services =>
            {
                services.AddScoped(_ => System.Reflection.DispatchProxy.Create<Simplic.OxS.ServiceDefinition.IAddonDefinitionRepository, StoresEverything>());
                services.AddDistributedMemoryCache();
            });
            using var client = host.GetTestClient();
            using var message = new HttpRequestMessage(HttpMethod.Post, "/cellar-api/v1/AddonDefinition")
            {
                Content = new StringContent("""{ "entity": "cellar.keg", "path": "handling", "kind": "string" }""", Encoding.UTF8, "application/json"),
            };

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Callers.Bearer());

            using var answer = await client.SendAsync(message);

            answer.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await answer.Content.ReadAsStringAsync()).Should().StartWith("'cellar.keg' is declared extendable but has no 'addon' member");
        }
    }

    /// <summary>An entity an organisation adds properties to.</summary>
    [OxQLType("cellar.vat", "cellar.vat", Extendable = true)]
    public class Vat : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public IDictionary<string, object> Addon { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>An entity that is declared extendable and has no member to hold what is added.</summary>
    [OxQLType("cellar.keg", "cellar.keg", Extendable = true)]
    public class Keg : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }
    }

    /// <summary>A service with both.</summary>
    public class CellarStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Cellar";

        protected override Type[] EntityTypes => [typeof(Vat), typeof(Keg)];

        protected override Type[] Controllers => [typeof(SchemaController), typeof(AddonDefinitionController)];
    }
}

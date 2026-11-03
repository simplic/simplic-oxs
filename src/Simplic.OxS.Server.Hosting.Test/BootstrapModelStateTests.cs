using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OxQL.Core.Engine;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// What a host built through <see cref="Bootstrap"/> says of its entity model to the query
    /// engine: degraded when the schema build failed and left a model without the service's
    /// entities, and not degraded for every model the service meant to build.
    /// </summary>
    public sealed class BootstrapModelStateTests
    {
        /// <summary>
        /// What the engine's model provider says. Read by name: the member came to the engine's
        /// interface after the version this package was first built against, and the provider
        /// says it to either.
        /// </summary>
        private static bool? DegradedOf(IHost host)
        {
            var provider = host.Services.GetRequiredService<IEntityModelProvider>();

            return (bool?)provider.GetType().GetProperty("Degraded")!.GetValue(provider);
        }

        [Fact]
        public async Task AHostWhoseSchemaBuildFailed_SaysItsModelIsDegraded()
        {
            using var host = await BootstrapHost.StartAsync<SalvageStartup>();

            var registry = host.Services.GetRequiredService<OxSchemaRegistry>();

            registry.Findings.Should().Contain(finding => finding.Code == OxSchemaCodes.EntityScanFailed);
            registry.Model.Entities.Should().BeEmpty();
            registry.Degraded.Should().BeTrue();
            DegradedOf(host).Should().BeTrue();
        }

        [Fact]
        public async Task AHostWithItsEntities_SaysItsModelIsNotDegraded()
        {
            using var host = await BootstrapHost.StartAsync<DispatchStartup>();

            var registry = host.Services.GetRequiredService<OxSchemaRegistry>();

            // Findings that leave the entities in the model do not degrade it.
            registry.Model.Entities.Should().NotBeEmpty();
            registry.Degraded.Should().BeFalse();
            DegradedOf(host).Should().BeFalse();
        }

        [Fact]
        public async Task AServiceWithoutEntities_SaysItsModelIsNotDegraded()
        {
            using var host = await BootstrapHost.StartAsync<NoticeStartup>();

            host.Services.GetRequiredService<OxSchemaRegistry>().Model.Entities.Should().BeEmpty();

            // Said, not left open: an engine that is told nothing takes a model without entities for a degraded one.
            DegradedOf(host).Should().BeFalse();
        }

        /// <summary>The health answer a contract 2 caller reads, anonymously.</summary>
        private static async Task<JsonNode> HealthAsync(IHost host, string api)
        {
            using var client = host.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/{api}/v1/OxQL/health");

            request.Headers.Add("X-OxQL-Contract", "2");

            using var answer = await client.SendAsync(request);

            var text = await answer.Content.ReadAsStringAsync();

            answer.StatusCode.Should().Be(HttpStatusCode.OK, text);

            return JsonNode.Parse(text)!;
        }

        [Fact]
        public async Task Health_OfAHostWhoseSchemaBuildFailed_IsDegradedAndSaysTheModelIs()
        {
            using var host = await BootstrapHost.StartAsync<SalvageQueryStartup>(new AnsweringEngine().Register);

            var health = await HealthAsync(host, "salvage-api");

            health["status"]!.GetValue<string>().Should().Be("degraded");
            health["model"]!.ToJsonString().Should().Be("""{"entities":0,"degraded":true,"reason":"build"}""");
        }

        [Fact]
        public async Task Health_OfAServiceWithoutEntities_IsHealthy()
        {
            using var host = await BootstrapHost.StartAsync<NoticeQueryStartup>(new AnsweringEngine().Register);

            var health = await HealthAsync(host, "notice-api");

            health["status"]!.GetValue<string>().Should().Be("healthy");
            health["model"]!.ToJsonString().Should().Be("""{"entities":0,"degraded":false}""");
        }

        [Fact]
        public async Task Health_OfAHostWithItsEntities_CountsThem()
        {
            using var host = await BootstrapHost.StartAsync<ParcelStartup>(new AnsweringEngine().Register);

            var health = await HealthAsync(host, "parcel-api");

            health["status"]!.GetValue<string>().Should().Be("healthy");
            health["model"]!.ToJsonString().Should().Be("""{"entities":1,"degraded":false}""");
        }
    }
}

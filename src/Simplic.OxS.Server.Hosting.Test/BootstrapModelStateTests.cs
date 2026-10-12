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
    }
}

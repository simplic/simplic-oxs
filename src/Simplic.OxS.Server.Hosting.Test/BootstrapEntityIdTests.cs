using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// Entity ids another service cannot reference: the host says so loudly and starts, in every
    /// environment and under continuous integration.
    /// </summary>
    public sealed class BootstrapEntityIdTests
    {
        [Theory]
        [InlineData("Development")]
        [InlineData("Local")]
        [InlineData("Staging")]
        [InlineData("Production")]
        public async Task AHostWithSuchIds_StartsAndLogsTheLineToPaste(string environment)
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<RegisterStartup>(log, environment);

            var registry = host.Services.GetRequiredService<OxSchemaRegistry>();

            registry.Document.Types.Keys.Should().Contain(["register_entry", "archive.register_page"]);
            registry.Findings.Select(finding => finding.Code).Should().Equal(OxSchemaCodes.EntityIdForeignService, OxSchemaCodes.EntityIdOffGrammar);

            var errors = log.Entries.Where(entry => entry.Level == LogLevel.Error).Select(entry => entry.Message).ToList();

            errors.Should().Contain(message => message.Contains("schema.RetireEntityId(\"register.register_entry\", \"register_entry\");"));
            errors.Should().Contain(message => message.Contains("schema.RetireEntityId(\"register.register_page\", \"archive.register_page\");"));
            errors.Should().ContainSingle(message => message.StartsWith("Ox schema: 2 entity ids are not <service>.<entity>"));
        }
    }
}

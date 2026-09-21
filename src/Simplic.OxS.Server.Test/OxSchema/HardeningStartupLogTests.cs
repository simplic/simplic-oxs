using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The level a finding is logged at when the host starts.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class HardeningStartupLogTests
    {
        private static HardeningCapturedLog LogOf(OxSchemaBuildOptions options)
        {
            var log = new HardeningCapturedLog();
            var services = new ServiceCollection();

            services.AddLogging(logging => logging.AddProvider(log));
            services.AddSingleton(OxSchemaRegistry.Build(options));

            using var provider = services.BuildServiceProvider();

            OxSchemaStartupLogger.Log(provider);

            return log;
        }

        [Fact]
        public void AServiceWithoutEntities_IsLoggedAsAWarningNotAnError()
        {
            var log = LogOf(SchemaBuild.OptionsWithoutAssemblies("Production"));

            log.Entries.Should().ContainSingle(entry => entry.Message.Contains(OxSchemaCodes.EntityAssembliesMissing))
                .Which.Level.Should().Be(LogLevel.Warning);
            log.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Error);
        }

        [Fact]
        public void AFailedScan_IsStillLoggedAsAnError()
        {
            var log = LogOf(SchemaBuild.OptionsWithUnloadableAssembly("Production"));

            log.Entries.Should().Contain(entry => entry.Level == LogLevel.Error && entry.Message.Contains(OxSchemaCodes.EntityScanFailed));
        }

        [Fact]
        public void ADroppedEntity_IsStillLoggedAsAnError()
        {
            var log = LogOf(SchemaBuild.Options("Production"));

            log.Entries.Should().Contain(entry => entry.Level == LogLevel.Error && entry.Message.Contains(OxSchemaCodes.DuplicateEntityId));
        }
    }
}

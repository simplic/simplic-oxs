using Simplic.OxS.Logging.Extension;

namespace Simplic.OxS.Server.Test.Extensions
{
    /// <summary>Checks the query engine's meter contract against the logging extension.</summary>
    public sealed class OxQLTelemetryIntegrationTests
    {
        [Fact]
        public void TheMeterName_IsTheOneTheQueryEngineNamesItsMeterBy()
        {
            // Read by name: the host is built against engine versions with and without the
            // constant, and exports the meter of either. One without it has nothing to compare.
            var named = typeof(global::OxQL.Core.Engine.IQueryEngine)
                .Assembly.GetType("OxQL.Core.Engine.OxQLMetrics")
                ?.GetField("MeterName")
                ?.GetRawConstantValue();

            if (named is not null)
                named.Should().Be(MonitoringExtension.QueryEngineMeterName);
        }
    }
}

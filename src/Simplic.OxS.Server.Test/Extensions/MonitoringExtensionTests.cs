using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Simplic.OxS.Server.Extensions;

namespace Simplic.OxS.Server.Test.Extensions
{
    /// <summary>Which meters a host with a <c>Monitoring</c> section exports.</summary>
    public sealed class MonitoringExtensionTests
    {
        /// <summary>Keeps the meter name of every metric it is handed.</summary>
        private sealed class MeterNames : BaseExporter<Metric>
        {
            public HashSet<string> Seen { get; } = [];

            public override ExportResult Export(in Batch<Metric> batch)
            {
                foreach (var metric in batch)
                    Seen.Add(metric.MeterName);

                return ExportResult.Success;
            }
        }

        [Fact]
        public void TheQueryEnginesMeter_IsAmongTheMeteredSources()
        {
            MonitoringExtension.QueryEngineMeterName.Should().Be("OxQL");

            // The section switches telemetry on; no exporter of the host's own is asked for, and the
            // endpoint is a local one nothing is sent to.
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoring:OtlpEndpoint"] = "http://localhost:4317",
                ["Monitoring:TracingExporter"] = "none",
                ["Monitoring:LoggingExporter"] = "none",
                ["Monitoring:MetricsExporter"] = "none",
            }).Build();

            var exported = new MeterNames();
            var services = new ServiceCollection();

            services.AddLoggingAndMetricTracing(configuration, "probe", "v1", "Production");
            services.ConfigureOpenTelemetryMeterProvider(builder => builder.AddReader(new BaseExportingMetricReader(exported)));

            using var provider = services.BuildServiceProvider();
            using var engine = new Meter("OxQL");
            using var unlisted = new Meter("probe.unlisted");

            var meters = provider.GetRequiredService<MeterProvider>();

            engine.CreateCounter<long>("oxql.requests").Add(1);
            unlisted.CreateCounter<long>("unlisted.requests").Add(1);
            meters.ForceFlush();

            exported.Seen.Should().Contain("OxQL").And.NotContain("probe.unlisted");
        }

        [Fact]
        public void TheMeterName_IsTheOneTheQueryEngineNamesItsMeterBy()
        {
            // Read by name: the host is built against engine versions with and without the
            // constant, and exports the meter of either. One without it has nothing to compare.
            var named = typeof(global::OxQL.Core.Engine.IQueryEngine).Assembly
                .GetType("OxQL.Core.Engine.OxQLMetrics")?
                .GetField("MeterName")?
                .GetRawConstantValue();

            if (named is not null)
                named.Should().Be(MonitoringExtension.QueryEngineMeterName);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// Add logging and tracing extension
    /// </summary>
    internal static class LoggingExtension
    {
        /// <summary>
        /// Add logging and metric/tracing to the service
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="serviceName">Actual service name</param>
        /// <returns>Service collection</returns>
        internal static IServiceCollection AddLoggingAndMetricTracing(this IServiceCollection services, string serviceName)
        {
            // TODO: Add OPTL, CorrelationId, UserId, TenantId

            services.AddOpenTelemetryTracing((builder) => builder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService($"Simplic.OxS.{serviceName}").AddTelemetrySdk())
                .AddAspNetCoreInstrumentation()
                .AddMassTransitInstrumentation()
                .AddConsoleExporter()
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri("http://otel-collector:4317");
                }));

            return services;
        }
    }
}

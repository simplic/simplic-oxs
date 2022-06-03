using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Simplic.OxS.Server.Extensions
{
    internal static class LoggingExtension
    {
        internal static IServiceCollection AddLoggingAndMetricTracing(this IServiceCollection services, string serviceName)
        {
            // TODO: Add OPTL, CorrelationId, UserId, TenantId

            services.AddOpenTelemetryTracing((builder) => builder
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService($"Simplic.OxS.{serviceName}"))
                .AddAspNetCoreInstrumentation()
                .AddMassTransitInstrumentation()
                .AddConsoleExporter()
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri("http://grafana:4317");
                }));

            return services;
        }
    }
}

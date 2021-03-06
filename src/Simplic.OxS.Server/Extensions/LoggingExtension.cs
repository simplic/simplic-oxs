using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Simplic.OxS.Server.Settings;
using StackExchange.Redis;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// Add logging and tracing extension
    /// </summary>
    internal static class MonitoringExtension
    {
        /// <summary>
        /// Add logging and metric/tracing to the service
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="serviceName">Actual service name</param>
        /// <returns>Service collection</returns>
        internal static IServiceCollection AddLoggingAndMetricTracing(this IServiceCollection services, IConfiguration configuration, string serviceName)
        {
            // Bind and read settings
            services.Configure<MonitoringSettings>(options => configuration.GetSection("Monitoring").Bind(options));
            var monitoringSettings = configuration.GetSection("Monitoring").Get<MonitoringSettings>();

            if (monitoringSettings == null)
            {
                Console.WriteLine("Could not find monitoring section in app-settings. Please ensure that a valid enviornment-name is passed (e.g. Local, Development, ...).");
                return services;
            }

            var resourceBuilder = ResourceBuilder.CreateDefault()
                                                 .AddService($"Simplic.OxS.{serviceName}")
                                                 .AddTelemetrySdk();

            services.AddOpenTelemetryTracing((builder) =>
            {
                builder.SetResourceBuilder(resourceBuilder)
                       .AddAspNetCoreInstrumentation(o =>
                       {
                           o.RecordException = true;
                       })
                       .AddMassTransitInstrumentation()
                       .AddHttpClientInstrumentation(o =>
                       {
                           o.RecordException = true;
                       })
                       .SetErrorStatusOnException(true);

                // Add redis instruments if connection is set
                var redisSettings = configuration.GetSection("Redis").Get<RedisSettings>();
                if (redisSettings != null && !string.IsNullOrWhiteSpace(redisSettings.RedisCacheUrl))
                {
                    var connection = ConnectionMultiplexer.Connect(redisSettings.RedisCacheUrl);
                    builder.AddRedisInstrumentation(connection);

                    Console.WriteLine("Add OpenTelemetry redis instruments");
                }

                if (UseConsoleExporter(monitoringSettings.TracingExporter) || string.IsNullOrWhiteSpace(monitoringSettings.OtlpEndpoint))
                    builder.AddConsoleExporter();

                if (UseOtlpExporter(monitoringSettings.TracingExporter) && !string.IsNullOrWhiteSpace(monitoringSettings.OtlpEndpoint))
                {
                    builder.AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri(monitoringSettings.OtlpEndpoint);
                    });
                }
            });

            // Add logging provider
            services.AddLogging(logging =>
            {
                logging.ClearProviders();

                logging.AddConsole();

                logging.AddOpenTelemetry(options =>
                 {
                     options.SetResourceBuilder(resourceBuilder);

                     options.IncludeScopes = true;
                     options.ParseStateValues = true;
                     options.IncludeFormattedMessage = true;

                     if (UseConsoleExporter(monitoringSettings.LoggingExporter) || string.IsNullOrWhiteSpace(monitoringSettings.OtlpEndpoint))
                         options.AddConsoleExporter();

                     if (UseOtlpExporter(monitoringSettings.LoggingExporter) && !string.IsNullOrWhiteSpace(monitoringSettings.OtlpEndpoint))
                     {
                         options.AddOtlpExporter(otlpOptions =>
                         {
                             otlpOptions.Endpoint = new Uri(monitoringSettings.OtlpEndpoint);
                         });
                     }
                 });
            });

            services.Configure<OpenTelemetryLoggerOptions>(opt =>
            {
                opt.IncludeScopes = true;
                opt.ParseStateValues = true;
                opt.IncludeFormattedMessage = true;
            });

            // Metrics
            services.AddOpenTelemetryMetrics(options =>
            {
                options.SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation();

                if (UseConsoleExporter(monitoringSettings.LoggingExporter) || string.IsNullOrWhiteSpace(monitoringSettings.OtlpEndpoint))
                    options.AddConsoleExporter();

                if (UseOtlpExporter(monitoringSettings.LoggingExporter) && !string.IsNullOrWhiteSpace(monitoringSettings.OtlpEndpoint))
                {
                    options.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(monitoringSettings.OtlpEndpoint);
                    });
                }

            });

            return services;
        }

        /// <summary>
        /// Determines whether console export should be used
        /// </summary>
        /// <param name="settings">Settings as string</param>
        /// <returns>True if console exporter should be used</returns>
        private static bool UseConsoleExporter(string settings) => settings?.ToLower()?.Contains("console") ?? false;

        /// <summary>
        /// Determines whether otlp export should be used
        /// </summary>
        /// <param name="settings">Settings as string</param>
        /// <returns>True if otlp exporter should be used</returns>
        private static bool UseOtlpExporter(string settings) => settings?.ToLower()?.Contains("otlp") ?? false;
    }
}

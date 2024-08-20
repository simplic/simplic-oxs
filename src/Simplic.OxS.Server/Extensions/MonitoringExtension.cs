using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Simplic.OxS.Server.Settings;

namespace Simplic.OxS.Server.Extensions;

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

        services.ConfigureOpenTelemetryTracerProvider((builder) =>
        {
            builder.SetResourceBuilder(resourceBuilder)
                .AddSource(serviceName)
                .AddHttpClientInstrumentation()
                .SetErrorStatusOnException(true);

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

            logging.AddSerilog(options =>
            {
                // use properties from log context
                options.Enrich.FromLogContext();

                options.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u4}{CallerColored}] {Message:lj}{NewLine}{Exception}",
                    theme: Themes.SerilogThemes.ConsoleTheme,
                    restrictedToMinimumLevel: LogEventLevel.Debug
                );

                options.WriteTo.OpenTelemetry(o =>
                {
                    o.Endpoint = monitoringSettings.OtlpEndpoint;
                    o.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
                });
            });

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
        services.ConfigureOpenTelemetryMeterProvider(options =>
        {
            options.SetResourceBuilder(resourceBuilder)
                .AddHttpClientInstrumentation();

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
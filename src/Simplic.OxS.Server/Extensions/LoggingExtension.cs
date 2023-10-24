using System.Diagnostics;
using MassTransit.Internals;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;
using Serilog.Core.Enrichers;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Simplic.OxS.Server.Extensions;

/// <summary>
/// Extension methods for logging.
/// </summary>
public static class LoggingExtension
{
    /// <summary>
    /// Add serilog to the logging pipeline.
    /// </summary>
    /// <param name="logging"></param>
    /// <param name="configure"></param>
    internal static ILoggingBuilder AddSerilog(this ILoggingBuilder logging, Action<LoggerConfiguration> configure)
    {
        var config = new LoggerConfiguration();
        configure.Invoke(config);

        return logging.AddSerilog(config.CreateLogger());
    }

    /// <summary>
    /// Writes a log message using the `Trace`/`Verbose` log level.
    /// Used for logging the most minor information.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="message"></param>
    /// <param name="args"></param>
    public static void Verbose(this ILogger logger, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogTrace(eventId, message, args);
        }
    }

    /// <summary>
    /// Writes a log message using the `Debug` log level.
    /// Used for debugging purposes.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="message"></param>
    /// <param name="args"></param>
    public static void Debug(this ILogger logger, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogDebug(eventId, message, args);
        }
    }

    /// <summary>
    /// Writes a log message using the `Information` log level.
    /// Used for general logging of noteworthy information.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="message"></param>
    /// <param name="args"></param>
    public static void Info(this ILogger logger, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogInformation(eventId, message, args);
        }
    }

    /// <summary>
    /// Writes a log message using the `Warning` log level.
    /// Used for logging unexpected or problematic situations.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="message"></param>
    /// <param name="args"></param>
    public static void Warn(this ILogger logger, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogWarning(eventId, message, args);
        }
    }

    /// <summary>
    /// Writes a log message using the `Error` log level.
    /// Used for logging exceptions.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="e"></param>
    /// <param name="message"></param>
    /// <param name="args"></param>
    public static void Error(
        this ILogger logger,
        Exception e,
        string message = "",
        params object?[] args
    )
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogError(eventId, e, message, args);
        }
    }

    /// <summary>
    /// Writes a log message using the `Critical`/`Fatal` log level.
    /// Used for logging the most severe errors that should be taken care of immediately.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="e"></param>
    /// <param name="message"></param>
    /// <param name="args"></param>
    public static void Crit(
        this ILogger logger,
        Exception e,
        string message = "",
        params object?[] args
    )
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogCritical(eventId, e, message, args);
        }
    }

    /// <summary>
    /// Converts a <see cref="LogLevel"/> to a <see cref="LogEventLevel"/>.
    /// </summary>
    /// <param name="logLevel"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static LogEventLevel ToLogEventLevel(this LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };

    /// <summary>
    /// Creates an <see cref="IDisposable"/> containing properties for serilog
    /// that involve caller information such as the class/type and function name.
    /// </summary>
    private static IDisposable CallerScope()
    {
        try
        {
            var callerMethod = new StackTrace().GetFrame(2)!.GetMethod();
            var callerType = callerMethod!.ReflectedType!.Name;
            var callerFn = callerMethod.Name;

            const string fnColor = "\u001b[33m";
            const string classColor = "\u001b[38;2;30;216;184m";
            const string noColor = "\u001b[0m";

            return LogContext.Push(
                new PropertyEnricher("Caller", $"{callerType}.{callerFn}"),
                new PropertyEnricher(
                    "CallerColored",
                    $"{classColor}{callerType}{noColor}.{fnColor}{callerFn}"
                )
            );
        }
        catch (Exception)
        {
            return LogContext.PushProperty("Caller", "");
        }
    }
}
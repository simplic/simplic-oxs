using System.Diagnostics;
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
    /// <param name="message">Message to log</param>
    /// <param name="args">Format arguments for message</param>
    public static void Verb(this ILogger logger, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogTrace(eventId, message, args);
        }
    }

    /// <inheritdoc cref="Verb"/>
    public static void Verb(this ILogger logger, Exception e, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogTrace(eventId, e, message, args);
        }
    }

    /// <summary>
    /// Writes a log message using the `Debug` log level.
    /// Used for debugging purposes.
    /// </summary>
    /// <param name="message">Message to log</param>
    /// <param name="args">Format arguments for message</param>
    public static void Debug(this ILogger logger, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogDebug(eventId, message, args);
        }
    }

    /// <inheritdoc cref="Debug(ILogger,string?,object?[])"/>
    public static void Debug(this ILogger logger, Exception e, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogDebug(eventId, e, message, args);
        }
    }

    /// <summary>
    /// Writes a log message using the `Information` log level.
    /// Used for general logging of noteworthy information.
    /// </summary>
    /// <param name="message">Message to log</param>
    /// <param name="args">Format arguments for message</param>
    public static void Info(this ILogger logger, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogInformation(eventId, message, args);
        }
    }

    /// <inheritdoc cref="Info(ILogger,string?,object?[])"/>
    public static void Info(this ILogger logger, Exception e, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogInformation(eventId, e, message, args);
        }
    }

    /// <summary>
    /// Writes a log message using the `Warning` log level.
    /// Used for logging unexpected or problematic situations.
    /// </summary>
    /// <param name="message">Message to log</param>
    /// <param name="args">Format arguments for message</param>
    public static void Warn(this ILogger logger, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogWarning(eventId, message, args);
        }
    }

    /// <inheritdoc cref="Warn(ILogger,string?,object?[])"/>
    public static void Warn(this ILogger logger, Exception e, string? message, params object?[] args)
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogWarning(eventId, e, message, args);
        }
    }

    /// <summary>
    /// Writes a log message using the `Error` log level.
    /// Used for logging exceptions.
    /// </summary>
    /// <param name="message">Message to log</param>
    /// <param name="args">Format arguments for message</param>
    public static void Error(
        this ILogger logger,
        string message = "",
        params object?[] args
    )
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogError(eventId, message, args);
        }
    }

    /// <inheritdoc cref="Error(ILogger,string,object?[])"/>
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
    /// <param name="message">Message to log</param>
    /// <param name="args">Format arguments for message</param>
    public static void Fatal(
        this ILogger logger,
        string message = "",
        params object?[] args
    )
    {
        var eventId = new EventId(-1, Guid.NewGuid().ToString());
        using (CallerScope())
        {
            logger.LogCritical(eventId, message, args);
        }
    }

    /// <inheritdoc cref="Fatal(ILogger,string,object?[])"/>
    public static void Fatal(
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
            LogLevel.None => throw new ArgumentException("LogLevel.None cannot be converted"),
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };

    /// <summary>
    /// Creates an <see cref="IDisposable"/> containing properties for serilog
    /// that involve caller information such as the class/type and function name.<br/>
    /// If it fails to obtain the caller scope, the properties will be left blank.<br/>
    /// </summary>
    /// <remarks>
    /// This function does not throw any exception and is therefore safe to call.
    /// </remarks>
    private static IDisposable CallerScope()
    {
        try
        {
            var stack = new StackTrace();

            /*
                When a stack trace is created in the same scope as the function/method
                the method will be at frame '0' for non-async methods.
                For async methods it will be at frame '3' due to async middleware being called.
                Since the caller (of which method we want) is calling the logging function (which adds one frame)
                and additionally the logging function calling this function (which adds another frame)
                the caller method will be at frame '2' for non-async and frame '5'  for async methods.
                Visual Example:
                    Sync
                        <SomeCaller.SomeFunc>
                            0: CallerScope()
                            1: logger.Warn(..)
                            2: SomeFunc()
                    Async
                        <SomeCaller.SomeAsyncFunc>
                            0: CallerScope()
                            1: logger.Warn(..)
                            2-4: async middleware
                            5: SomeAsyncFunc()
            */
            const int frameOffset = 2;
            const int frameOffsetAsync = frameOffset + 3;

            /*
                If the caller method is async, we would expect the function name `MoveNext` at frame '2'
                and the type `AsyncMethodBuilderCore` at frame '3'
                [POTENTIAL BUG]
                    If this would for some reason not be the case, although the method being async
                    then this code will be invalid.
                    A reason could be that microsoft changed one of those names.
            */
            var isAsync = stack.GetFrame(frameOffset)?.GetMethod()?.Name == "MoveNext" &&
                          stack.GetFrame(frameOffset + 1)?.GetMethod()?.ReflectedType?.Name == "AsyncMethodBuilderCore";

            var callerFrame = isAsync ? stack.GetFrame(frameOffsetAsync)! : stack.GetFrame(frameOffset)!;
            var callerMethod = callerFrame.GetMethod()!;
            var callerType = callerMethod.ReflectedType!.Name;
            var callerFn = callerMethod.Name;
            if (callerFn.StartsWith('.'))
                callerFn = callerFn[1..];

            const string fnColor = "\u001b[33m";
            const string classColor = "\u001b[38;2;30;216;184m";
            const string exitColor = "\u001b[0m";

            return LogContext.Push(
                new PropertyEnricher("Caller", $" {callerType}.{callerFn}"),
                new PropertyEnricher(
                    "CallerColored",
                    $" {classColor}{callerType}{exitColor}.{fnColor}{callerFn}"
                )
            );
        }
        catch (Exception)
        {
            return LogContext.Push(
                new PropertyEnricher("Caller", ""),
                new PropertyEnricher("CallerColored", "")
            );
        }
    }
}
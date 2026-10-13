using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>A logger provider that keeps what a host logs, for a test to read back.</summary>
    internal sealed class CapturedLog : ILoggerProvider
    {
        /// <summary>Every entry, in the order it was logged.</summary>
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) => new Sink(this);

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        private sealed class Sink(CapturedLog owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}

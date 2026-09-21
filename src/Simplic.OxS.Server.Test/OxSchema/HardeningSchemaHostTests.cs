using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OxQL.Core.Engine;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>
    /// A schema build that throws must not stop a host outside fail-fast: the host starts, the
    /// schema endpoint says what happened, and every other route answers as before.
    /// </summary>
    [Collection(SchemaCollection.Name)]
    public sealed class HardeningSchemaHostTests
    {
        private const string Secret = "the scan blew up at C:\\build\\Fictional.Driver.dll";

        private static readonly Type[] Controllers = [typeof(SchemaController), typeof(ModelDefinitionController)];

        /// <summary>The fixture host's inputs, with an assembly list that throws the moment the build reads it.</summary>
        private static void Throwing(OxSchemaOptionsBuilder schema)
        {
            HardeningTestHost.Lenient(schema);
            schema.TypeAssemblies = new ThrowingList();
        }

        private static Task<IHost> StartAsync(Action<OxSchemaOptionsBuilder> configure, CapturedLog? log = null) =>
            HardeningTestHost.StartAsync(Controllers, services =>
            {
                services.AddSchemaControllerServices();
                services.AddOxSchema(configure);

                if (log is not null)
                    services.AddLogging(logging => logging.AddProvider(log));
            });

        [Fact]
        public async Task AThrowingBuild_OutsideFailFast_StillStartsTheHost()
        {
            using var host = await StartAsync(Throwing);

            host.Services.GetRequiredService<OxSchemaRegistry>().Document.Types.Should().BeEmpty();
        }

        [Fact]
        public async Task AThrowingBuild_OutsideFailFast_ServesTheScanFailedDiagnostic()
        {
            using var host = await StartAsync(Throwing);

            var response = await host.GetTestClient().GetAsync("/schema");
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(body);
            var diagnostic = document.RootElement.GetProperty("diagnostics").EnumerateArray().Should().ContainSingle().Subject;

            diagnostic.GetProperty("code").GetString().Should().Be(OxSchemaCodes.EntityScanFailed);
            diagnostic.GetProperty("target").GetString().Should().Be(SchemaBuild.Service);
            document.RootElement.GetProperty("types").EnumerateObject().Should().BeEmpty();
            document.RootElement.GetProperty("revision").GetString().Should().StartWith("sha256:");
        }

        [Fact]
        public async Task AThrowingBuild_OutsideFailFast_KeepsTheExceptionOffTheWire()
        {
            using var host = await StartAsync(Throwing);

            var body = await host.GetTestClient().GetStringAsync("/schema");

            body.Should().NotContain("Fictional").And.NotContain(nameof(NotSupportedException));
        }

        [Fact]
        public async Task AThrowingBuild_OutsideFailFast_StillServesTheModelDefinition()
        {
            using var host = await StartAsync(Throwing);

            var response = await host.GetTestClient().GetAsync("/ModelDefinition");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(SchemaBuild.Degraded.ModelDefinition!.Body);
        }

        [Fact]
        public async Task AThrowingBuild_OutsideFailFast_HandsTheQueryEngineAnEmptyModel()
        {
            using var host = await StartAsync(Throwing);

            host.Services.GetRequiredService<IEntityModelProvider>().Model.Entities.Should().BeEmpty();
        }

        [Fact]
        public async Task AThrowingBuild_OutsideFailFast_IsLoggedCriticalWithTheException()
        {
            var log = new CapturedLog();

            using var host = await StartAsync(Throwing, log);

            log.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Critical)
                .Which.Exception.Should().BeOfType<NotSupportedException>().Which.Message.Should().Be(Secret);
        }

        [Fact]
        public async Task AThrowingBuild_UnderFailFast_StopsTheHost()
        {
            var start = () => StartAsync(schema =>
            {
                Throwing(schema);
                schema.EnvironmentName = "Development";
            });

            (await start.Should().ThrowAsync<NotSupportedException>()).WithMessage(Secret);
        }

        [Fact]
        public async Task AHealthyBuild_ServesTheSameDocumentAsBefore()
        {
            using var host = await StartAsync(HardeningTestHost.Lenient);

            var body = await host.GetTestClient().GetByteArrayAsync("/schema");

            body.Should().Equal(SchemaBuild.Build("Production").Body);
        }

        [Fact]
        public void ANullControllerEntry_DropsThatEntryOnly()
        {
            var options = SchemaBuild.Options();

            var registry = SchemaBuild.Build(options with { ControllerTypes = [.. options.ControllerTypes, null!] });

            registry.ModelDefinition!.DefinitionCount.Should().Be(options.ControllerTypes.Count);
            registry.ModelDefinition.Failures.Should().ContainSingle().Which.Should().StartWith("(null): ");
            registry.Document.Types.Keys.Should().Equal(SchemaBuild.Degraded.Document.Types.Keys);
        }

        /// <summary>A list that throws on every read: an unexpected exception from inside the build.</summary>
        private sealed class ThrowingList : IReadOnlyList<Assembly>
        {
            public Assembly this[int index] => throw new NotSupportedException(Secret);

            public int Count => throw new NotSupportedException(Secret);

            public IEnumerator<Assembly> GetEnumerator() => throw new NotSupportedException(Secret);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class CapturedLog : ILoggerProvider
        {
            public ConcurrentQueue<(LogLevel Level, Exception? Exception)> Entries { get; } = new();

            public ILogger CreateLogger(string categoryName) => new Sink(this);

            public void Dispose()
            {
            }

            private sealed class Sink(CapturedLog owner) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state)
                    where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                    owner.Entries.Enqueue((logLevel, exception));
            }
        }
    }
}

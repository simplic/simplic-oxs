using System.Collections;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OxQL.Core.Engine;
using OxQL.Studio;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>
    /// A schema build that throws must not stop a host outside fail-fast: the host starts, the
    /// schema endpoint says what happened, and every other route answers as before. The OxQL
    /// Studio console the host serves beside it sits at <c>/oxql</c> under the path base.
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

        private static Task<IHost> StartAsync(Action<OxSchemaOptionsBuilder> configure, HardeningCapturedLog? log = null) =>
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
            var log = new HardeningCapturedLog();

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

        private const string PathBase = "/vehicle-api/v1";

        /// <summary>A host that serves only the console, configured as the base package configures it, under a service's path base.</summary>
        private static async Task<IHost> StartStudioAsync(IReadOnlyDictionary<string, string?>? settings = null)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build();

            var host = new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddOxQLStudio(options => Bootstrap.ConfigureOxQLStudio(options, configuration));
                    })
                    .Configure(app =>
                    {
                        app.UsePathBase(PathBase);
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapOxQLStudio());
                    }))
                .Build();

            await host.StartAsync();

            return host;
        }

        [Fact]
        public async Task TheStudio_UnderAPathBase_ServesItsShellAtOxQL_WithEveryPathPrefixed()
        {
            using var host = await StartStudioAsync();

            var response = await host.GetTestClient().GetAsync($"{PathBase}/oxql");
            var html = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            html.Should().Contain($"\"apiBasePath\":\"{PathBase}/oxql\"")
                .And.Contain($"\"schemaBasePath\":\"{PathBase}/schema\"")
                .And.Contain($"\"assetBasePath\":\"{PathBase}/oxql\"")
                .And.Contain("\"enableExplain\":true");
        }

        [Fact]
        public async Task TheStudio_UnderAPathBase_ServesItsAssetsBesideTheShell()
        {
            using var host = await StartStudioAsync();

            var response = await host.GetTestClient().GetAsync($"{PathBase}/oxql/app.js");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task TheStudio_IsNoLongerServedUnderTheDoubledPathBase()
        {
            using var host = await StartStudioAsync();

            var response = await host.GetTestClient().GetAsync($"{PathBase}{PathBase}/oxql");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        public void TheStudiosExplainButton_FollowsTheEngine_WhichIsOnUnlessSwitchedOff(string? configured, bool enabled)
        {
            var options = new OxQLStudioOptions();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OxQL:Explain:Enabled"] = configured }).Build();

            Bootstrap.ConfigureOxQLStudio(options, configuration);

            options.EnableExplain.Should().Be(enabled);
            options.RoutePath.Should().Be("/oxql");
            options.ApiBasePath.Should().Be("/oxql");
            options.SchemaBasePath.Should().Be("/schema");
        }

        /// <summary>A list that throws on every read: an unexpected exception from inside the build.</summary>
        private sealed class ThrowingList : IReadOnlyList<Assembly>
        {
            public Assembly this[int index] => throw new NotSupportedException(Secret);

            public int Count => throw new NotSupportedException(Secret);

            public IEnumerator<Assembly> GetEnumerator() => throw new NotSupportedException(Secret);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}

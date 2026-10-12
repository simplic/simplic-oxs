using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxQL;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>What a host says at start about the owners its references point into, and how it connects to them.</summary>
    public sealed class BootstrapOwnerRouteTests
    {
        private static readonly (string Key, string Value)[] Owners =
        [
            ("InternalHosts:carpool", "carpool-host:8080"),
            ("InternalApiVersions:carpool", "v2"),
            ("InternalHosts:crew", "crew-host"),

            // Configured and referenced by nothing: no line.
            ("InternalHosts:archive", "archive-host"),
        ];

        [Fact]
        public async Task TheStart_LogsTheRouteOfEveryConfiguredOwnerAReferencePointsInto_AndWhereItsVersionComesFrom()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<DispatchStartup>(log, settings: Owners);

            var lines = log.Entries.Where(entry => entry.Message.StartsWith("OxQL owner ", StringComparison.Ordinal)).ToList();

            lines.Should().OnlyContain(entry => entry.Level == LogLevel.Information);
            lines.Select(entry => entry.Message).Should().Equal(
                "OxQL owner carpool: http://carpool-host:8080/carpool-api/v2/internal/oxql/batch (api version v2 from InternalApiVersions:carpool)",
                "OxQL owner crew: http://crew-host/crew-api/v1/internal/oxql/batch (api version v1, the default: no InternalApiVersions:crew entry)");
        }

        [Fact]
        public async Task AnOwnerEntryThatFormsNoAddress_IsNamedAsAWarning()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<DispatchStartup>(log, settings: [("InternalHosts:carpool", "not a host"), ("InternalHosts:crew", "crew-host")]);

            log.Entries.Should().ContainSingle(entry => entry.Message.StartsWith("OxQL owner carpool: ", StringComparison.Ordinal))
                .Which.Should().Match<(LogLevel Level, string Message)>(entry =>
                    entry.Level == LogLevel.Warning && entry.Message.Contains("'InternalHosts' entry does not form a valid address"));
        }

        [Fact]
        public async Task TheOwnerClient_IsWhatTheQueryEnginesReferenceCheckReadsTheOwnersSchemaDocumentsThrough()
        {
            using var host = await BootstrapHost.StartAsync<DispatchStartup>(settings: Owners);

            var client = host.Services.GetRequiredService<global::OxQL.Core.Engine.IRemoteQueryClient>();

            client.Should().BeAssignableTo<global::OxQL.Core.Engine.IRemoteSchemaSource>();
            host.Services.GetService<global::OxQL.Core.Engine.RemoteReferenceValidator>().Should().NotBeNull("the engine registers the check the client is read by");
            ((RemoteQueryClient)client).SchemaUrl("carpool").Should().Be("http://carpool-host:8080/carpool-api/v2/schema");
        }

        /// <summary>The connect timeout of the handler the host gives the named client.</summary>
        private static TimeSpan ConnectTimeoutOf(IHost host)
        {
            var handler = host.Services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(RemoteQueryClient.HttpClientName);

            while (handler is DelegatingHandler delegating)
                handler = delegating.InnerHandler!;

            return handler.Should().BeOfType<SocketsHttpHandler>().Which.ConnectTimeout;
        }

        [Fact]
        public async Task TheOwnerClient_GivesUpConnectingAfterTwoSeconds_AndKeepsTheDefaultOverallTimeout()
        {
            using var host = await BootstrapHost.StartAsync<DispatchStartup>(settings: Owners);

            RemoteQueryClient.DefaultConnectTimeout.Should().Be(TimeSpan.FromSeconds(2));
            ConnectTimeoutOf(host).Should().Be(RemoteQueryClient.DefaultConnectTimeout);

            // Every owner call brings its own budget as a token; the client's timeout is not what bounds one.
            using var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient(RemoteQueryClient.HttpClientName);

            client.Timeout.Should().Be(TimeSpan.FromSeconds(100));
        }

        [Fact]
        public async Task TheOwnerClient_GivesUpConnectingAfterTheTimeTheHostConfigures()
        {
            using var host = await BootstrapHost.StartAsync<DispatchStartup>(settings: [.. Owners, (RemoteQueryClient.ConnectTimeoutKey, "350")]);

            RemoteQueryClient.ConnectTimeoutKey.Should().Be("OxQL:Remote:ConnectTimeoutMs");
            ConnectTimeoutOf(host).Should().Be(TimeSpan.FromMilliseconds(350));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        public async Task TheOwnerClient_SetsNoConnectTimeoutOfItsOwn_WhereTheHostConfiguresZeroOrLess(string configured)
        {
            using var host = await BootstrapHost.StartAsync<DispatchStartup>(settings: [.. Owners, (RemoteQueryClient.ConnectTimeoutKey, configured)]);

            ConnectTimeoutOf(host).Should().Be(Timeout.InfiniteTimeSpan);
        }
    }
}

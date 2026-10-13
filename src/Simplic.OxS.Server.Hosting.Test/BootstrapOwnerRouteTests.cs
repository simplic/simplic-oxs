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

        /// <summary>What the start says of an owner whose entry is no host, after the reason.</summary>
        private const string NotUsed =
            ". A value is host or host:port, where host is a name of letters, digits, dots, hyphens and underscores, an IPv4 address or an IPv6 address in brackets, and port a number from 1 to 65535; "
            + "no scheme, no user, no path, no query. Until it is one, the service is not configured on this host: no call is sent to it, and every reference into it resolves to nothing.";

        [Theory]
        [InlineData("http://carpool-host:8080", "it names a scheme")]
        [InlineData("https://carpool.example.com", "it names a scheme")]
        [InlineData("carpool-host/", "it has a path or a query")]
        [InlineData("carpool-host:8080/base?x=1", "it has a path or a query")]
        [InlineData("user@elsewhere.example.com", "it carries user information")]
        [InlineData("user:secret@carpool-host", "it carries user information")]
        [InlineData("not a host", "it contains whitespace")]
        [InlineData("carpool-host:port", "it holds a character a host name has none of")]
        [InlineData("carpool-host:0", "its port is no number from 1 to 65535")]
        public async Task AnOwnerEntryThatIsNoHost_IsNamedAsAnErrorWithTheKeyAndTheForm_AndTheOwnerIsNotConfigured(string entry, string defect)
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<DispatchStartup>(log, settings: [("InternalHosts:carpool", entry), ("InternalHosts:crew", "crew-host"), ("InternalHosts:billing", "billing-host")]);

            var line = log.Entries.Should().ContainSingle(logged => logged.Message.StartsWith("OxQL owner carpool: ", StringComparison.Ordinal)).Which;

            line.Level.Should().Be(LogLevel.Error);
            line.Message.Should().Be($"OxQL owner carpool: the value of InternalHosts:carpool is not used, because {defect}{NotUsed}");

            // Nothing the host logs repeats the value, which may hold a credential.
            log.Entries.Should().NotContain(logged => logged.Message.Contains(entry, StringComparison.Ordinal) || logged.Message.Contains("secret", StringComparison.Ordinal));

            var client = (RemoteQueryClient)host.Services.GetRequiredService<global::OxQL.Core.Engine.IRemoteQueryClient>();

            client.IsConfigured("carpool").Should().BeFalse();
            client.BatchUrl("carpool").Should().BeNull();
            client.IsConfigured("crew").Should().BeTrue();

            // The engine's own check then names the owner among those the host has not configured.
            log.Entries.Should().Contain(logged => logged.Level == LogLevel.Error && logged.Message.StartsWith("OxQL remote reference finding: service 'carpool' is not configured on this host", StringComparison.Ordinal));
        }

        [Fact]
        public async Task AnEntryThatIsNoHost_OfAServiceNothingReferences_IsNotNamed()
        {
            var log = new CapturedLog();

            using var host = await BootstrapHost.StartAsync<DispatchStartup>(log, settings: [.. Owners, ("InternalHosts:billing", "billing-host"), ("InternalHosts:archive", "http://archive-host")]);

            log.Entries.Should().NotContain(logged => logged.Message.Contains("archive", StringComparison.Ordinal));
            log.Entries.Should().NotContain(logged => logged.Level >= LogLevel.Warning && logged.Message.StartsWith("OxQL", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("Development")]
        [InlineData("Local")]
        public async Task AnOwnerEntryThatIsNoHost_StopsTheStartWhereAMissingEntryDoes_WithTheSentenceOfItsLine(string environment)
        {
            (string Key, string Value)[] settings = [("InternalHosts:carpool", "http://carpool-host:8080"), ("InternalHosts:crew", "crew-host"), ("InternalHosts:billing", "billing-host")];

            using var host = BootstrapHost.Build<DispatchStartup>(new CapturedLog(), environment, settings);

            var start = () => host.StartAsync();

            (await start.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be(
                $"OxQL refuses to start: OxQL owner carpool: the value of InternalHosts:carpool is not used, because it names a scheme{NotUsed}"
                + " To start without them, set OxQL:Startup:StrictRemoteReferences to false.");

            // What switches the engine's check of the remote references off switches this one off.
            using var lenient = await BootstrapHost.StartAsync<DispatchStartup>(new CapturedLog(), environment, [.. settings, ("OxQL:Startup:StrictRemoteReferences", "false")]);

            lenient.Services.GetRequiredService<global::OxQL.Core.Engine.IRemoteQueryClient>().IsConfigured("carpool").Should().BeFalse();
        }

        [Fact]
        public async Task TheOwnerClient_FollowsNoRedirect()
        {
            using var host = await BootstrapHost.StartAsync<DispatchStartup>(settings: Owners);

            var handler = host.Services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(RemoteQueryClient.HttpClientName);

            while (handler is DelegatingHandler delegating)
                handler = delegating.InnerHandler!;

            handler.Should().BeOfType<SocketsHttpHandler>().Which.AllowAutoRedirect.Should().BeFalse();
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

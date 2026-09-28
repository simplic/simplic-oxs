using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxQL.AspNetCore;
using OxQL.Core.Engine;
using OxQL.Core.Models;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>
    /// The remote calls are always bounded in time, a host entry that forms no address is an
    /// unreachable owner, every call goes to the owner's route under <c>RouteOf</c> with the internal
    /// key and the contract header and nothing else, and the owner's shallow health is kept.
    /// </summary>
    public sealed class HardeningRemoteQueryClientTests
    {
        /// <summary>An owner that never answers: the call ends only when its token is cancelled.</summary>
        private sealed class SilentHandler : HttpMessageHandler
        {
            public int Calls { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;

                await Task.Delay(Timeout.Infinite, cancellationToken);

                throw new InvalidOperationException("The wait above only ends by cancellation.");
            }
        }

        private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        }

        /// <summary>An owner that answers every call with one status and body, and keeps what it was sent.</summary>
        private sealed class AnsweringHandler(HttpStatusCode status, string body) : HttpMessageHandler
        {
            public List<(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string Body)> Sent { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase);
                var content = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);

                Sent.Add((request.Method, request.RequestUri!, headers, content));

                return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }
        }

        private static RemoteQueryClient Client(HttpMessageHandler handler, string host, string? version = null)
        {
            var settings = new Dictionary<string, string?> { ["InternalHosts:vehicle"] = host };

            if (version is not null)
                settings["InternalApiVersions:vehicle"] = version;

            return new(
                new Factory(handler),
                Options.Create(new AuthSettings { InternalApiKey = "key" }),
                new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
                new HttpContextAccessor(),
                NullLogger<RemoteQueryClient>.Instance);
        }

        private static ExplainRequest Explain() => new()
        {
            Query = new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] },
            Remote = ExplainRequest.RemoteSkip,
            IsEnvelope = true,
        };

        private static BatchRequest Batch() => new() { Queries = [new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] }] };

        [Theory(Timeout = 10_000)]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Batch_WithoutAPositiveBudget_IsStillBounded(int budgetMs)
        {
            var handler = new SilentHandler();
            var client = Client(handler, "localhost:8080");
            client.FallbackBudget = TimeSpan.FromMilliseconds(50);

            var call = () => client.BatchAsync("vehicle", Batch(), TimeSpan.FromMilliseconds(budgetMs), CancellationToken.None);

            await call.Should().ThrowAsync<OperationCanceledException>();
            handler.Calls.Should().Be(1);
        }

        [Fact]
        public void TheFallbackBudget_IsTheQueryEnginesDefaultCeiling()
        {
            Client(new SilentHandler(), "localhost:8080").FallbackBudget.Should().Be(TimeSpan.FromMilliseconds(new ExecutionOptions().MaxTimeMs));
        }

        [Theory]
        [InlineData("vehicle:not-a-port")]
        [InlineData("[::1")]
        public async Task Batch_ToAHostEntryThatFormsNoAddress_FailsAsAnUnreachableOwner(string host)
        {
            var handler = new SilentHandler();
            var client = Client(handler, host);

            var call = () => client.BatchAsync("vehicle", Batch(), TimeSpan.FromSeconds(1), CancellationToken.None);

            await call.Should().ThrowAsync<HttpRequestException>();
            handler.Calls.Should().Be(0);
        }

        [Theory]
        [InlineData("vehicle:not-a-port")]
        [InlineData("[::1")]
        public async Task IsReachable_ForAHostEntryThatFormsNoAddress_IsFalse(string host)
        {
            var handler = new SilentHandler();

            (await Client(handler, host).IsReachableAsync("vehicle", CancellationToken.None)).Should().BeFalse();
            handler.Calls.Should().Be(0);
        }

        [Theory]
        [InlineData(null, "http://vehicle-svc:8080/vehicle-api/v1/")]
        [InlineData("v2", "http://vehicle-svc:8080/vehicle-api/v2/")]
        public void RouteOf_IsTheOwnersBaseRoute_AndEveryRouteLiesUnderIt(string? version, string route)
        {
            var client = Client(new SilentHandler(), "vehicle-svc:8080", version);

            client.RouteOf("vehicle").Should().Be(route);
            client.BatchUrl("vehicle").Should().Be(route + "internal/oxql/batch");
            client.ExplainUrl("vehicle").Should().Be(route + "internal/oxql/explain");
            client.HealthUrl("vehicle").Should().Be(route + "OxQL/health?shallow=true");
            client.RouteOf("unknown").Should().BeNull();
            client.ExplainUrl("unknown").Should().BeNull();
        }

        [Fact]
        public async Task Explain_PostsTheEnvelopeToTheOwnersInternalExplain_WithTheKeyAndTheContractHeaderOnly()
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"valid":true,"contract":2,"describe":[]}""");

            var answer = await Client(handler, "vehicle-svc:8080").ExplainAsync("vehicle", Explain(), TimeSpan.FromSeconds(1), CancellationToken.None);

            answer!["valid"]!.GetValue<bool>().Should().BeTrue();

            var sent = handler.Sent.Should().ContainSingle().Subject;
            sent.Method.Should().Be(HttpMethod.Post);
            sent.Uri.Should().Be(new Uri("http://vehicle-svc:8080/vehicle-api/v1/internal/oxql/explain"));
            sent.Headers.Keys.Should().BeEquivalentTo(["Authorization", OxQLQueryService.ContractHeader]);
            sent.Headers["Authorization"].Should().Be("i-api-key key");
            sent.Headers[OxQLQueryService.ContractHeader].Should().Be(EngineCapabilities.Contract.ToString());

            var body = JsonNode.Parse(sent.Body)!;
            body["query"]!["entityType"]!.GetValue<string>().Should().Be("vehicle.vehicle");
            body["remote"]!.GetValue<string>().Should().Be(ExplainRequest.RemoteSkip);
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task Explain_AnsweredWithAnythingButSuccess_Throws(HttpStatusCode status)
        {
            var handler = new AnsweringHandler(status, "{}");

            var call = () => Client(handler, "vehicle-svc:8080").ExplainAsync("vehicle", Explain(), TimeSpan.FromSeconds(1), CancellationToken.None);

            (await call.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(status);
        }

        [Theory(Timeout = 10_000)]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Explain_WithoutAPositiveBudget_IsStillBounded(int budgetMs)
        {
            var handler = new SilentHandler();
            var client = Client(handler, "vehicle-svc:8080");
            client.FallbackBudget = TimeSpan.FromMilliseconds(50);

            var call = () => client.ExplainAsync("vehicle", Explain(), TimeSpan.FromMilliseconds(budgetMs), CancellationToken.None);

            await call.Should().ThrowAsync<OperationCanceledException>();
            handler.Calls.Should().Be(1);
        }

        [Fact]
        public async Task Explain_ForAServiceThisHostDoesNotKnow_IsACallerError()
        {
            var call = () => Client(new SilentHandler(), "vehicle-svc:8080").ExplainAsync("unknown", Explain(), TimeSpan.FromSeconds(1), CancellationToken.None);

            await call.Should().ThrowAsync<InvalidOperationException>();
        }

        [Theory]
        [InlineData(null, 1000, 1000)]
        [InlineData(400, 1000, 400)]
        [InlineData(5000, 1000, 1000)]
        [InlineData(0, 1000, 1000)]
        public async Task Batch_CarriesTheOwnersCeiling_TheSmallerOfTheEnginesAndTheBudget(int? requested, int budgetMs, int sent)
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"results":[]}""");
            var batch = Batch() with { MaxTimeMs = requested };

            await Client(handler, "vehicle-svc:8080").BatchAsync("vehicle", batch, TimeSpan.FromMilliseconds(budgetMs), CancellationToken.None);

            var call = handler.Sent.Should().ContainSingle().Subject;
            call.Uri.Should().Be(new Uri("http://vehicle-svc:8080/vehicle-api/v1/internal/oxql/batch"));
            call.Headers.Keys.Should().BeEquivalentTo(["Authorization", OxQLQueryService.ContractHeader]);
            JsonNode.Parse(call.Body)!["maxTimeMs"]!.GetValue<int>().Should().Be(sent);
        }

        [Fact]
        public async Task OwnerOf_IsUnknownUntilTheOwnersShallowHealthWasRead()
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"status":"ok","engine":{"version":"2.1.0","contract":2},"limits":{"maxBatchQueries":16}}""");
            var client = Client(handler, "vehicle-svc:8080");

            client.Should().BeAssignableTo<IRemoteOwnerInfo>();
            client.OwnerOf("vehicle").Should().BeNull();

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeTrue();

            client.OwnerOf("vehicle").Should().Be(new RemoteOwnerInfo("2.1.0", 2, 16));
            handler.Sent.Should().ContainSingle().Which.Uri.Should().Be(new Uri("http://vehicle-svc:8080/vehicle-api/v1/OxQL/health?shallow=true"));
            client.OwnerOf("unknown").Should().BeNull();
        }

        [Fact]
        public async Task IsReachable_WithAHealthBodyThatIsNotJson_IsTrue_AndLearnsNothing()
        {
            var client = Client(new AnsweringHandler(HttpStatusCode.OK, "not json"), "vehicle-svc:8080");

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeTrue();
            client.OwnerOf("vehicle").Should().BeNull();
        }

        [Fact]
        public async Task IsReachable_ForAnOwnerAnsweringAnError_IsFalse_AndLearnsNothing()
        {
            var client = Client(new AnsweringHandler(HttpStatusCode.ServiceUnavailable, """{"engine":{"version":"2.1.0"}}"""), "vehicle-svc:8080");

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeFalse();
            client.OwnerOf("vehicle").Should().BeNull();
        }
    }
}

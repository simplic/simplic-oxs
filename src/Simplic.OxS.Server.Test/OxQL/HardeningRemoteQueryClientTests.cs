using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxQL.AspNetCore;
using OxQL.AspNetCore.Scope;
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

        /// <summary>
        /// An owner that answers every call with one status and body, and keeps what it was sent.
        /// Given <paramref name="health"/>, its shallow health answers that with
        /// <paramref name="healthStatus"/> (200 by default) instead.
        /// </summary>
        private sealed class AnsweringHandler(HttpStatusCode status, string body, string? health = null, HttpStatusCode healthStatus = HttpStatusCode.OK) : HttpMessageHandler
        {
            public List<(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string Body)> Sent { get; } = [];

            /// <summary>The posts, without the health reads.</summary>
            public IEnumerable<(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, string Body)> Posts => Sent.Where(sent => sent.Method == HttpMethod.Post);

            /// <summary>The health reads.</summary>
            public int HealthReads => Sent.Count(sent => sent.Method == HttpMethod.Get);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase);
                var content = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);

                Sent.Add((request.Method, request.RequestUri!, headers, content));

                var probe = request.Method == HttpMethod.Get && health is not null;

                return new HttpResponseMessage(probe ? healthStatus : status)
                {
                    Content = new StringContent(probe ? health! : Answer(content), Encoding.UTF8, "application/json"),
                };
            }

            /// <summary>The batch body, or for <c>echo</c> one result per query naming its entity, in order.</summary>
            private string Answer(string sent) =>
                body != "echo"
                    ? body
                    : new JsonObject { ["results"] = new JsonArray([.. JsonNode.Parse(sent)!["queries"]!.AsArray().Select(query => (JsonNode?)JsonValue.Create(query!["entityType"]!.GetValue<string>()))]) }.ToJsonString();
        }

        /// <summary>A health answer whose body fails while it is read.</summary>
        private sealed class BrokenBodyHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new BrokenContent() });

            private sealed class BrokenContent : HttpContent
            {
                protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
                    throw new IOException("The connection was reset while the body was read.");

                protected override bool TryComputeLength(out long length)
                {
                    length = 0;
                    return false;
                }
            }
        }

        /// <summary>A health answer whose body never arrives: the read ends only by cancellation.</summary>
        private sealed class StalledBodyHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StalledContent() });

            private sealed class StalledContent : HttpContent
            {
                protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);

                    throw new InvalidOperationException("The wait above only ends by cancellation.");
                }

                protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
                    throw new InvalidOperationException("The body is only read as a stream.");

                protected override bool TryComputeLength(out long length)
                {
                    length = 0;
                    return false;
                }
            }
        }

        /// <summary>A clock a test moves by hand.</summary>
        private sealed class ManualTime(DateTimeOffset start) : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = start;

            public override DateTimeOffset GetUtcNow() => Now;
        }

        /// <summary>The identity the engine scoped the parent query with.</summary>
        private sealed class Scope(Guid organisation) : IOxQLScopeProvider
        {
            public ValueTask<Guid?> OrganisationAsync(HttpContext? httpContext, CancellationToken cancellationToken) => ValueTask.FromResult<Guid?>(organisation);

            public string? UserId(HttpContext? httpContext) => "user-7";

            public string? CorrelationId(HttpContext? httpContext) => "corr-42";
        }

        private static RemoteQueryClient Client(HttpMessageHandler handler, string host, string? version = null, HttpContext? request = null)
        {
            var settings = new Dictionary<string, string?> { ["InternalHosts:vehicle"] = host };

            if (version is not null)
                settings["InternalApiVersions:vehicle"] = version;

            return new(
                new Factory(handler),
                Options.Create(new AuthSettings { InternalApiKey = "key" }),
                new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
                new HttpContextAccessor { HttpContext = request },
                NullLogger<RemoteQueryClient>.Instance);
        }

        private const string Health16 = """{"status":"ok","engine":{"version":"2.1.0","contract":2},"limits":{"maxBatchQueries":16}}""";

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
        public async Task Batch_CarriesTheOwnersCeiling_TheSmallerOfTheEnginesAndTheBudget_AndNoOtherMember(int? requested, int budgetMs, int sent)
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"results":[]}""");
            var batch = Batch() with { MaxTimeMs = requested };

            await Client(handler, "vehicle-svc:8080").BatchAsync("vehicle", batch, TimeSpan.FromMilliseconds(budgetMs), CancellationToken.None);

            var call = handler.Posts.Should().ContainSingle().Subject;
            call.Uri.Should().Be(new Uri("http://vehicle-svc:8080/vehicle-api/v1/internal/oxql/batch"));
            call.Headers.Keys.Should().BeEquivalentTo(["Authorization", OxQLQueryService.ContractHeader]);
            JsonNode.Parse(call.Body)!["maxTimeMs"]!.GetValue<int>().Should().Be(sent, "the engine already writes its ceiling below its wait, so no second margin");
            JsonNode.Parse(call.Body)!.AsObject().Select(member => member.Key).Should().BeEquivalentTo(["queries", "maxTimeMs"], "the owner refuses any other batch member");
            handler.HealthReads.Should().Be(0, "the engine asks for the owner's facts before it sends; a batch sends only itself");
        }

        [Theory]
        [InlineData(null, "v1")]
        [InlineData("v2", "v2")]
        public void ApiVersionOf_ThroughTheOwnerInterface_IsTheConfiguredVersionElseV1_AndNullForAnUnknownService(string? configured, string expected)
        {
            IRemoteOwnerInfo client = Client(new SilentHandler(), "vehicle-svc:8080", configured);

            client.ApiVersionOf("vehicle").Should().Be(expected);
            client.ApiVersionOf("unknown").Should().BeNull();
        }

        [Fact]
        public async Task OwnerOfAsync_ForAnUnknownOwner_ReadsItsShallowHealthOnce_ThroughTheOwnerInterface()
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"results":[]}""", """{"engine":{"version":"2.1.0","contract":2},"limits":{"maxBatchQueries":16,"maxPageSize":500}}""");
            IRemoteOwnerInfo client = Client(handler, "vehicle-svc:8080");

            var first = await client.OwnerOfAsync("vehicle", CancellationToken.None);
            var second = await client.OwnerOfAsync("vehicle", CancellationToken.None);

            first.Should().Be(new RemoteOwnerInfo("2.1.0", 2, 16, 500));
            second.Should().Be(first);
            handler.HealthReads.Should().Be(1);
            (await client.OwnerOfAsync("unknown", CancellationToken.None)).Should().BeNull();
            handler.HealthReads.Should().Be(1, "a service without a host entry is never probed");
        }

        [Fact]
        public async Task OwnerOfAsync_ForAnOwnerWhoseHealthSaysNothing_IsNull_AndNotProbedAgainWithinTheTtl()
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"results":[]}""", "", HttpStatusCode.NotFound);
            var time = new ManualTime(DateTimeOffset.UnixEpoch);
            var client = Client(handler, "vehicle-svc:8080");
            client.Time = time;

            (await client.OwnerOfAsync("vehicle", CancellationToken.None)).Should().BeNull();
            (await client.OwnerOfAsync("vehicle", CancellationToken.None)).Should().BeNull();
            handler.HealthReads.Should().Be(1);

            time.Now += client.FactsTtl;
            await client.OwnerOfAsync("vehicle", CancellationToken.None);

            handler.HealthReads.Should().Be(2, "a probe that learned nothing is tried again once the time to live has passed");
        }

        [Fact]
        public async Task OwnerOf_OlderThanTheTtl_IsUnknownAgain_AndOwnerOfAsyncReadsTheHealthAgain()
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"results":[]}""", Health16);
            var time = new ManualTime(DateTimeOffset.UnixEpoch);
            var client = Client(handler, "vehicle-svc:8080");
            client.Time = time;

            await client.OwnerOfAsync("vehicle", CancellationToken.None);
            time.Now += client.FactsTtl - TimeSpan.FromMilliseconds(1);
            client.OwnerOf("vehicle").Should().NotBeNull();

            time.Now += TimeSpan.FromMilliseconds(1);
            client.OwnerOf("vehicle").Should().BeNull("a rolled-back owner is not taken for the engine it ran before");

            (await client.OwnerOfAsync("vehicle", CancellationToken.None)).Should().NotBeNull();
            handler.HealthReads.Should().Be(2);
        }

        [Fact(Timeout = 10_000)]
        public async Task OwnerOfAsync_WhenTheCallerStopsWaiting_IsCancelled_AndTheProbeServesTheNextCaller()
        {
            var handler = new SilentHandler();
            var client = Client(handler, "vehicle-svc:8080");
            client.HealthBudget = TimeSpan.FromMilliseconds(200);
            using var impatient = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

            var first = async () => await client.OwnerOfAsync("vehicle", impatient.Token);
            await first.Should().ThrowAsync<OperationCanceledException>();

            (await client.OwnerOfAsync("vehicle", CancellationToken.None)).Should().BeNull();
            handler.Calls.Should().Be(1, "the second caller waited on the probe the first one started");
        }

        [Fact]
        public async Task Batch_RefusedByTheOwner_ThrowsWithTheOwnersCodeAndMessage()
        {
            var refusal = """{"type":"validation_error","title":"The request could not be bound.","errors":[{"code":"BATCH_TOO_LARGE","message":"The batch carries 20 queries; the limit is 16."}]}""";
            var handler = new AnsweringHandler(HttpStatusCode.BadRequest, refusal);

            var call = () => Client(handler, "vehicle-svc:8080").BatchAsync("vehicle", Batch(), TimeSpan.FromSeconds(1), CancellationToken.None);

            var thrown = (await call.Should().ThrowAsync<HttpRequestException>()).Which;
            thrown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            thrown.Message.Should().Contain("BATCH_TOO_LARGE").And.Contain("The batch carries 20 queries; the limit is 16.");
        }

        [Fact]
        public async Task Explain_RefusedByTheOwnerWithoutErrors_ThrowsWithTheReasonClassAndTitle()
        {
            var handler = new AnsweringHandler(HttpStatusCode.RequestEntityTooLarge, """{"type":"validation_error","title":"The request is too large."}""");

            var call = () => Client(handler, "vehicle-svc:8080").ExplainAsync("vehicle", Explain(), TimeSpan.FromSeconds(1), CancellationToken.None);

            (await call.Should().ThrowAsync<HttpRequestException>()).Which.Message.Should().Contain("validation_error").And.Contain("The request is too large.");
        }

        [Fact]
        public async Task Explain_ForwardsTheScopedIdentity_BesideTheKeyAndTheContractHeader()
        {
            var organisation = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var request = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection()
                    .AddSingleton<IOxQLScopeProvider>(new Scope(organisation))
                    .BuildServiceProvider(),
            };
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"valid":true,"contract":2,"describe":[]}""");

            await Client(handler, "vehicle-svc:8080", request: request).ExplainAsync("vehicle", Explain(), TimeSpan.FromSeconds(1), CancellationToken.None);

            var sent = handler.Posts.Should().ContainSingle().Subject;
            sent.Headers.Keys.Should().BeEquivalentTo(
            [
                "Authorization",
                OxQLQueryService.ContractHeader,
                Constants.HttpHeaderOrganizationIdKey,
                Constants.HttpHeaderUserIdKey,
                Constants.HttpHeaderCorrelationIdKey,
            ]);
            sent.Headers[Constants.HttpHeaderOrganizationIdKey].Should().Be(organisation.ToString());
            sent.Headers[Constants.HttpHeaderUserIdKey].Should().Be("user-7");
            sent.Headers[Constants.HttpHeaderCorrelationIdKey].Should().Be("corr-42");
        }

        [Fact]
        public async Task IsReachable_WhenTheHealthBodyFailsWhileRead_IsTrue_AndLearnsNothing()
        {
            var client = Client(new BrokenBodyHandler(), "vehicle-svc:8080");

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeTrue("the owner answered");
            client.OwnerOf("vehicle").Should().BeNull();
        }

        [Fact(Timeout = 10_000)]
        public async Task IsReachable_WhenTheHealthBodyDoesNotArriveInTime_IsTrue_AndLearnsNothing()
        {
            var client = Client(new StalledBodyHandler(), "vehicle-svc:8080");
            client.HealthBudget = TimeSpan.FromMilliseconds(50);

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeTrue("the owner answered");
            client.OwnerOf("vehicle").Should().BeNull();
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

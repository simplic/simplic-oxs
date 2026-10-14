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
            private readonly TaskCompletionSource<CancellationToken> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public int Calls { get; private set; }

            /// <summary>The token of the first call, once it is waiting.</summary>
            public Task<CancellationToken> Entered => entered.Task;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                entered.TrySetResult(cancellationToken);

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
            private readonly TaskCompletionSource<CancellationToken> reading = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>The token the body is read with, once the read is waiting.</summary>
            public Task<CancellationToken> Reading => reading.Task;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StalledContent(reading) });

            private sealed class StalledContent(TaskCompletionSource<CancellationToken> reading) : HttpContent
            {
                protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
                {
                    reading.TrySetResult(cancellationToken);

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

        [Fact]
        public async Task ACallOfAServiceThatKnowsItsName_NamesItAsTheCaller()
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"answers":[{"valid":true,"contract":2,"stages":[]}]}""");
            var client = Client(handler, "vehicle-svc:8080");
            client.Caller = "logistics";

            await client.ExplainAsync("vehicle", Explain(), TimeSpan.FromSeconds(1), CancellationToken.None);

            handler.Sent.Should().ContainSingle().Which.Headers[RemoteQueryClient.CallerHeader].Should().Be("logistics");
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

        private const string Health16 = """{"status":"ok","engine":{"version":"9.9.9","contract":2},"limits":{"maxBatchQueries":16}}""";

        private static ExplainRequest Explain() => new()
        {
            Query = new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] },
            Remote = ExplainRequest.RemoteCheck,
            Budget = new ExplainBudget(750, 3),
            IsEnvelope = true,
        };

        private static BatchRequest Batch() => new() { Queries = [new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] }] };

        [Theory(Timeout = 10_000)]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Batch_WithoutAPositiveBudget_IsStillBounded(int budgetMs)
        {
            var handler = new SilentHandler();
            var time = new ManualTime(DateTimeOffset.UnixEpoch);
            var client = Client(handler, "localhost:8080");
            client.Time = time;
            client.FallbackBudget = TimeSpan.FromMilliseconds(50);

            var call = client.BatchAsync("vehicle", Batch(), TimeSpan.FromMilliseconds(budgetMs), CancellationToken.None);
            var sent = await handler.Entered;

            time.Advance(client.FallbackBudget - TimeSpan.FromMilliseconds(1));
            sent.IsCancellationRequested.Should().BeFalse("the call has its fallback budget");

            time.Advance(TimeSpan.FromMilliseconds(1));
            sent.IsCancellationRequested.Should().BeTrue("the fallback budget has passed");

            await call.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
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
        [InlineData("http://vehicle-svc:8080")]
        [InlineData("user:secret@vehicle-svc")]
        public async Task Batch_ToAHostEntryThatIsNoHost_IsACallToAServiceThatIsNotConfigured_AndSendsNothing(string host)
        {
            var handler = new SilentHandler();
            var client = Client(handler, host);

            var batch = () => client.BatchAsync("vehicle", Batch(), TimeSpan.FromSeconds(1), CancellationToken.None);
            var explain = () => client.ExplainAsync("vehicle", Explain(), TimeSpan.FromSeconds(1), CancellationToken.None);

            foreach (var call in new Func<Task>[] { batch, explain })
            {
                var thrown = (await call.Should().ThrowAsync<InvalidOperationException>()).Which;

                thrown.Message.Should().StartWith("The 'InternalHosts' entry for 'vehicle' is not used, because ").And.EndWith("the service is not configured on this host.");
                thrown.Message.Should().NotContain(host).And.NotContain("secret");
            }

            handler.Calls.Should().Be(0);
        }

        /// <summary>An owner that answers every call with a redirect to another host.</summary>
        private sealed class RedirectingHandler(HttpStatusCode status) : HttpMessageHandler
        {
            public int Calls { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;

                var answer = new HttpResponseMessage(status);

                answer.Headers.Location = new Uri("http://elsewhere.example/vehicle-api/v1/internal/oxql/batch");

                return Task.FromResult(answer);
            }
        }

        [Theory]
        [InlineData(HttpStatusCode.MovedPermanently)]
        [InlineData(HttpStatusCode.Found)]
        [InlineData(HttpStatusCode.TemporaryRedirect)]
        [InlineData(HttpStatusCode.PermanentRedirect)]
        public async Task ARedirectOfAnOwner_IsAnOwnerThatDoesNotServeTheRoute_AndIsNotFollowed(HttpStatusCode status)
        {
            var handler = new RedirectingHandler(status);
            var client = Client(handler, "vehicle-svc:8080");

            var batch = () => client.BatchAsync("vehicle", Batch(), TimeSpan.FromSeconds(1), CancellationToken.None);
            var explain = () => client.ExplainAsync("vehicle", Explain(), TimeSpan.FromSeconds(1), CancellationToken.None);

            foreach (var (call, route) in new (Func<Task> Call, string Route)[] { (batch, "batch"), (explain, "explain") })
            {
                var thrown = (await call.Should().ThrowAsync<HttpRequestException>()).Which;

                // What the query engine reads as an owner without the route.
                thrown.StatusCode.Should().Be(HttpStatusCode.NotFound);
                thrown.Message.Should().Be($"The owner of 'vehicle' answered the internal {route} with a redirect ({(int)status}), which is not followed: it does not serve the route at the configured address.");
            }

            handler.Calls.Should().Be(2, "one call per route, and none to where the answer points");
            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeFalse();
            (await client.SchemaEntitiesAsync("vehicle", null, CancellationToken.None)).Should().BeNull();
            handler.Calls.Should().Be(4);
        }

        [Theory]
        [InlineData("vehicle-svc")]
        [InlineData("vehicle-svc:8080")]
        [InlineData("Vehicle.Svc.cluster.local:80")]
        [InlineData("vehicle_svc")]
        [InlineData("vehicle-svc.namespace.svc.cluster.local.")]
        [InlineData("vehicle-svc.:8080")]
        [InlineData("localhost:65535")]
        [InlineData("10.42.0.7")]
        [InlineData("10.42.0.7:8080")]
        [InlineData("[::1]")]
        [InlineData("[fd00::17]:8080")]
        [InlineData("  vehicle-svc:8080  ")]
        public void AHostEntry_IsAHostWithAnOptionalPort(string host)
        {
            var client = Client(new SilentHandler(), host);

            RemoteQueryClient.HostEntryDefect(host.Trim()).Should().BeNull();
            client.IsConfigured("vehicle").Should().BeTrue();
            client.RefusedHostOf("vehicle").Should().BeNull();
            client.BatchUrl("vehicle").Should().Be($"http://{host.Trim()}/vehicle-api/v1/internal/oxql/batch");
            Uri.TryCreate(client.BatchUrl("vehicle"), UriKind.Absolute, out var address).Should().BeTrue();
            address!.UserInfo.Should().BeEmpty();
            address.AbsolutePath.Should().Be("/vehicle-api/v1/internal/oxql/batch");
        }

        [Theory]
        [InlineData("http://vehicle-svc:8080", "it names a scheme")]
        [InlineData("https://vehicle.example.com", "it names a scheme")]
        [InlineData("vehicle-svc/", "it has a path or a query")]
        [InlineData("vehicle-svc:8080/base", "it has a path or a query")]
        [InlineData("vehicle-svc?x=1", "it has a path or a query")]
        [InlineData("vehicle-svc#x", "it has a path or a query")]
        [InlineData("vehicle-svc\\base", "it has a path or a query")]
        [InlineData("user@elsewhere.example.com", "it carries user information")]
        [InlineData("user:secret@vehicle-svc:8080", "it carries user information")]
        [InlineData("not a host", "it contains whitespace")]
        [InlineData("vehicle-svc :8080", "it contains whitespace")]
        [InlineData("vehicle\tsvc", "it contains whitespace")]
        [InlineData("vehicle:not-a-port", "it holds a character a host name has none of")]
        [InlineData("[::1", "it holds a character a host name has none of")]
        [InlineData("vehicle-svc:8080:9090", "it holds a character a host name has none of")]
        [InlineData("vehicle%2Dsvc", "it holds a character a host name has none of")]
        [InlineData("-vehicle", "it holds a character a host name has none of")]
        [InlineData("vehicle-svc..", "it holds a character a host name has none of")]
        [InlineData(".", "it holds a character a host name has none of")]
        [InlineData("vehicle-svc:", "its port is no number from 1 to 65535")]
        [InlineData("vehicle-svc:0", "its port is no number from 1 to 65535")]
        [InlineData("vehicle-svc:65536", "its port is no number from 1 to 65535")]
        [InlineData("vehicle-svc:99999999999", "its port is no number from 1 to 65535")]
        [InlineData("[1:2]", "it forms no address")]
        public async Task AHostEntryOfAnotherForm_IsNotConfigured_AndNeverCalled(string host, string defect)
        {
            var handler = new SilentHandler();
            var client = Client(handler, host);

            RemoteQueryClient.HostEntryDefect(host).Should().Be(defect);
            client.RefusedHostOf("vehicle").Should().Be(defect);
            client.IsConfigured("vehicle").Should().BeFalse();
            client.RouteOf("vehicle").Should().BeNull();
            client.BatchUrl("vehicle").Should().BeNull();
            client.ExplainUrl("vehicle").Should().BeNull();
            client.HealthUrl("vehicle").Should().BeNull();
            client.SchemaUrl("vehicle").Should().BeNull();
            client.ApiVersionOf("vehicle").Should().BeNull();

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeFalse();
            (await client.OwnerOfAsync("vehicle", CancellationToken.None)).Should().BeNull();
            (await client.SchemaEntitiesAsync("vehicle", null, CancellationToken.None)).Should().BeNull();
            handler.Calls.Should().Be(0);
        }

        [Fact]
        public async Task AnOwnerThatAnswers404ToItsHealth_IsAnOwnerWithoutTheRoute_ThrownWithTheStatus()
        {
            var client = Client(new AnsweringHandler(HttpStatusCode.NotFound, ""), "vehicle-svc:8080");

            var ask = () => client.IsReachableAsync("vehicle", CancellationToken.None);
            var thrown = (await ask.Should().ThrowAsync<HttpRequestException>()).Which;

            thrown.StatusCode.Should().Be(HttpStatusCode.NotFound);
            thrown.Message.Should().Be("The owner of 'vehicle' answered 404 to its health route: it does not serve the query routes at the configured address.");
            client.OwnerOf("vehicle").Should().BeNull();

            // Before a batch the same owner is one whose facts are not known: the batch reports what it meets, and nothing is thrown here.
            (await client.OwnerOfAsync("vehicle", CancellationToken.None)).Should().BeNull();
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task AnOwnerThatAnswers404ToABatchOrAnExplain_IsThrownWithTheStatus(HttpStatusCode status)
        {
            var client = Client(new AnsweringHandler(status, ""), "vehicle-svc:8080");

            var batch = () => client.BatchAsync("vehicle", Batch(), TimeSpan.FromSeconds(1), CancellationToken.None);
            var explain = () => client.ExplainBatchAsync("vehicle", new ExplainBatchRequest { Checks = [Explain()] }, TimeSpan.FromSeconds(1), CancellationToken.None);

            (await batch.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(status);
            (await explain.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(status);
        }

        [Fact]
        public void TheFormOfAHostEntry_IsSaidInOneSentence()
        {
            RemoteQueryClient.HostEntryForm.Should().Be(
                "host or host:port, where host is a name of letters, digits, dots, hyphens and underscores, an IPv4 address or an IPv6 address in brackets, and port a number from 1 to 65535; no scheme, no user, no path, no query");
            RemoteQueryClient.HostsSection.Should().Be("InternalHosts");
        }

        /// <summary>
        /// An owner nobody listens at: what the connection pool ends a call with when its connect
        /// timeout elapses, a cancellation that wraps a <see cref="TimeoutException"/> while the
        /// call's own token is not cancelled.
        /// </summary>
        private sealed class NoConnectionHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                throw new TaskCanceledException("The operation was canceled.", new TimeoutException("A connection could not be established within the configured ConnectTimeout."));
        }

        [Fact]
        public async Task Batch_ToAnOwnerThatAcceptsNoConnection_FailsAsAnUnreachableOwner_WhileTheCallStillHasBudget()
        {
            var client = Client(new NoConnectionHandler(), "localhost:8080");

            var call = () => client.BatchAsync("vehicle", Batch(), TimeSpan.FromSeconds(30), CancellationToken.None);

            var thrown = (await call.Should().ThrowAsync<HttpRequestException>()).Which;

            thrown.StatusCode.Should().BeNull("no owner answered");
            thrown.Message.Should().Be("The owner of 'vehicle' accepted no connection for the internal batch in time.");
            thrown.InnerException.Should().BeOfType<TaskCanceledException>();
        }

        [Fact]
        public async Task Explain_ToAnOwnerThatAcceptsNoConnection_FailsAsAnUnreachableOwner()
        {
            var client = Client(new NoConnectionHandler(), "localhost:8080");

            var call = () => client.ExplainAsync("vehicle", Explain(), TimeSpan.FromSeconds(30), CancellationToken.None);

            (await call.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().BeNull();
        }

        [Fact]
        public async Task IsReachable_ForAnOwnerThatAcceptsNoConnection_IsFalse()
        {
            (await Client(new NoConnectionHandler(), "localhost:8080").IsReachableAsync("vehicle", CancellationToken.None)).Should().BeFalse();
        }

        [Fact]
        public void TheApiVersionSource_IsTheConfigurationKey_OrTheDefault()
        {
            Client(new SilentHandler(), "localhost:8080", "v2").ApiVersionSourceOf("vehicle").Should().Be("InternalApiVersions:vehicle");
            Client(new SilentHandler(), "localhost:8080").ApiVersionSourceOf("vehicle").Should().Be("default");
            Client(new SilentHandler(), "localhost:8080", " ").ApiVersionSourceOf("vehicle").Should().Be("default", "a blank entry is no entry");
            Client(new SilentHandler(), "localhost:8080").ApiVersionSourceOf("hr").Should().BeNull("the host does not know the service");
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
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"answers":[{"valid":true,"contract":2,"stages":[]}]}""");

            var answer = await Client(handler, "vehicle-svc:8080").ExplainAsync("vehicle", Explain(), TimeSpan.FromSeconds(1), CancellationToken.None);

            answer!["valid"]!.GetValue<bool>().Should().BeTrue();

            var sent = handler.Sent.Should().ContainSingle().Subject;
            sent.Method.Should().Be(HttpMethod.Post);
            sent.Uri.Should().Be(new Uri("http://vehicle-svc:8080/vehicle-api/v1/internal/oxql/explain"));
            sent.Headers.Keys.Should().BeEquivalentTo(["Authorization", OxQLQueryService.ContractHeader]);
            sent.Headers["Authorization"].Should().Be("i-api-key key");
            sent.Headers[OxQLQueryService.ContractHeader].Should().Be(EngineCapabilities.Contract.ToString());

            // One request is a batch of one check; what the origin has left is the batch's.
            var body = JsonNode.Parse(sent.Body)!.AsObject();
            body.Select(member => member.Key).Should().Equal("checks", "budget");
            var check = body["checks"]!.AsArray().Should().ContainSingle().Subject!;
            check["query"]!["entityType"]!.GetValue<string>().Should().Be("vehicle.vehicle");
            check["remote"]!.GetValue<string>().Should().Be(ExplainRequest.RemoteCheck);
            check.AsObject().ContainsKey("budget").Should().BeFalse();
            body["budget"]!.ToJsonString().Should().Be("""{"ms":750,"calls":3}""");
        }

        [Fact]
        public async Task ExplainBatch_PostsEveryCheckOfARoundInOneCall_AndAnswersThemInOrder()
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"answers":[{"valid":true,"contract":2},null,{"valid":false,"contract":2}]}""");
            var batch = new ExplainBatchRequest
            {
                Checks = [Explain() with { Budget = null }, Explain() with { Budget = null }, Explain() with { Budget = null }],
                Budget = new ExplainBudget(500, 2),
            };

            var answers = await Client(handler, "vehicle-svc:8080").ExplainBatchAsync("vehicle", batch, TimeSpan.FromSeconds(1), CancellationToken.None);

            answers.Should().HaveCount(3);
            answers![0]!["valid"]!.GetValue<bool>().Should().BeTrue();
            answers[1].Should().BeNull("the owner left that check unanswered");
            answers[2]!["valid"]!.GetValue<bool>().Should().BeFalse();

            var sent = handler.Sent.Should().ContainSingle("one call carries the round").Subject;
            sent.Uri.Should().Be(new Uri("http://vehicle-svc:8080/vehicle-api/v1/internal/oxql/explain"));

            var body = JsonNode.Parse(sent.Body)!;
            body["checks"]!.AsArray().Should().HaveCount(3);
            body["budget"]!.ToJsonString().Should().Be("""{"ms":500,"calls":2}""");
        }

        [Theory]
        [InlineData("""{"answers":[{"valid":true}]}""")]
        [InlineData("""{"answers":[]}""")]
        [InlineData("""{"valid":true,"contract":2}""")]
        public async Task ExplainBatch_AnsweredWithAnotherNumberOfAnswersThanChecks_Throws(string answered)
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, answered);
            var batch = new ExplainBatchRequest { Checks = [Explain() with { Budget = null }, Explain() with { Budget = null }] };

            var call = () => Client(handler, "vehicle-svc:8080").ExplainBatchAsync("vehicle", batch, TimeSpan.FromSeconds(1), CancellationToken.None);

            await call.Should().ThrowAsync<HttpRequestException>("an answer that cannot be matched to the checks answers none of them");
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
            var time = new ManualTime(DateTimeOffset.UnixEpoch);
            var client = Client(handler, "vehicle-svc:8080");
            client.Time = time;
            client.FallbackBudget = TimeSpan.FromMilliseconds(50);

            var call = client.ExplainAsync("vehicle", Explain(), TimeSpan.FromMilliseconds(budgetMs), CancellationToken.None);
            var sent = await handler.Entered;

            time.Advance(client.FallbackBudget - TimeSpan.FromMilliseconds(1));
            sent.IsCancellationRequested.Should().BeFalse("the call has its fallback budget");

            time.Advance(TimeSpan.FromMilliseconds(1));
            sent.IsCancellationRequested.Should().BeTrue("the fallback budget has passed");

            await call.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
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
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"results":[]}""", """{"engine":{"version":"9.9.9","contract":2},"limits":{"maxBatchQueries":16,"maxPageSize":500}}""");
            IRemoteOwnerInfo client = Client(handler, "vehicle-svc:8080");

            var first = await client.OwnerOfAsync("vehicle", CancellationToken.None);
            var second = await client.OwnerOfAsync("vehicle", CancellationToken.None);

            first.Should().Be(new RemoteOwnerInfo("9.9.9", 2, 16, 500));
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
            var time = new ManualTime(DateTimeOffset.UnixEpoch);
            var client = Client(handler, "vehicle-svc:8080");
            client.Time = time;
            using var impatient = new CancellationTokenSource();

            var first = client.OwnerOfAsync("vehicle", impatient.Token).AsTask();
            var probe = await handler.Entered;
            impatient.Cancel();

            await first.Awaiting(task => task).Should().ThrowAsync<OperationCanceledException>();
            probe.IsCancellationRequested.Should().BeFalse("the probe runs on its own clock, not on the caller's token");

            (await client.OwnerOfAsync("vehicle", CancellationToken.None)).Should().BeNull();
            handler.Calls.Should().Be(1, "the probe the first caller started serves the next one");

            time.Advance(client.HealthBudget);
            probe.IsCancellationRequested.Should().BeTrue("the probe is bounded by the health budget");
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
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"answers":[{"valid":true,"contract":2}]}""");

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
            var handler = new StalledBodyHandler();
            var time = new ManualTime(DateTimeOffset.UnixEpoch);
            var client = Client(handler, "vehicle-svc:8080");
            client.Time = time;

            var reachable = client.IsReachableAsync("vehicle", CancellationToken.None);
            var read = await handler.Reading;

            time.Advance(client.HealthBudget - TimeSpan.FromMilliseconds(1));
            read.IsCancellationRequested.Should().BeFalse("the body has the rest of the health budget");

            time.Advance(TimeSpan.FromMilliseconds(1));

            (await reachable).Should().BeTrue("the owner answered");
            client.OwnerOf("vehicle").Should().BeNull();
        }

        [Fact]
        public async Task OwnerOf_IsUnknownUntilTheOwnersShallowHealthWasRead()
        {
            var handler = new AnsweringHandler(HttpStatusCode.OK, """{"status":"ok","engine":{"version":"9.9.9","contract":2},"limits":{"maxBatchQueries":16}}""");
            var client = Client(handler, "vehicle-svc:8080");

            client.Should().BeAssignableTo<IRemoteOwnerInfo>();
            client.OwnerOf("vehicle").Should().BeNull();

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeTrue();

            client.OwnerOf("vehicle").Should().Be(new RemoteOwnerInfo("9.9.9", 2, 16));
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

        /// <summary>An owner that answers every call with <paramref name="status"/> and a body whose charset no reader knows.</summary>
        private sealed class BogusCharsetHandler(HttpStatusCode status) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = new ByteArrayContent(Encoding.UTF8.GetBytes("""{"status":"ok"}"""));
                content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=bogus");

                return Task.FromResult(new HttpResponseMessage(status) { Content = content });
            }
        }

        [Fact]
        public async Task IsReachable_WithAHealthBodyInAnUnknownCharset_IsTrue_AndNeverThrows()
        {
            var client = Client(new BogusCharsetHandler(HttpStatusCode.OK), "vehicle-svc:8080");

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeTrue();
            client.OwnerOf("vehicle").Should().BeNull();
        }

        [Fact]
        public async Task Batch_RefusedWithABodyInAnUnknownCharset_StillThrowsWithTheOwnersStatus()
        {
            var call = () => Client(new BogusCharsetHandler(HttpStatusCode.Forbidden), "vehicle-svc:8080").BatchAsync("vehicle", Batch(), TimeSpan.FromSeconds(1), CancellationToken.None);

            (await call.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task IsReachable_ForAnOwnerAnsweringAnError_IsFalse_AndLearnsNothing()
        {
            var client = Client(new AnsweringHandler(HttpStatusCode.ServiceUnavailable, """{"engine":{"version":"9.9.9"}}"""), "vehicle-svc:8080");

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeFalse();
            client.OwnerOf("vehicle").Should().BeNull();
        }
    }
}

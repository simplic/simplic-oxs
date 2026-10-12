using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OxQL.AspNetCore;
using OxQL.AspNetCore.Batch;
using OxQL.Core.Engine;
using OxQL.Core.Models;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.Test.OxSchema;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>
    /// The guard of every internal controller, exercised through a running host: only the
    /// configured key passes, a host whose key is blank is closed, and a refusal never runs
    /// the action; the OxQL internal batch and explain routes are guarded the same way.
    /// </summary>
    public sealed class HardeningInternalApiKeyTests
    {
        private const string Key = "c0ffee00-1111-2222-3333-444455556666";
        private const string Unauthorized = "Internal-api-key (i-api-key) is not valid or not provided";

        private static Task<IHost> StartAsync(string? configuredKey, ProbeCalls? calls = null) =>
            HardeningTestHost.StartAsync([typeof(HardeningInternalProbeController), typeof(HardeningPlainProbeController)], services =>
            {
                services.AddSingleton(calls ?? new ProbeCalls());
                services.Configure<AuthSettings>(settings => settings.InternalApiKey = configuredKey!);
            });

        /// <summary>Sends the header exactly as given; an <see cref="HttpClient"/> would normalise it first.</summary>
        private static async Task<(int Status, string Body)> GetAsync(IHost host, string path, string? authorization)
        {
            var context = await host.GetTestServer().SendAsync(request =>
            {
                request.Request.Method = HttpMethods.Get;
                request.Request.Path = path;

                if (authorization is not null)
                    request.Request.Headers.Authorization = authorization;
            });

            using var reader = new StreamReader(context.Response.Body);

            return (context.Response.StatusCode, await reader.ReadToEndAsync());
        }

        [Fact]
        public async Task TheCorrectKey_IsAdmitted()
        {
            using var host = await StartAsync(Key);

            var (status, body) = await GetAsync(host, "/internal/hardening-probe", $"i-api-key {Key}");

            status.Should().Be((int)HttpStatusCode.OK);
            body.Should().Be("reached");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("i-api-key")]
        [InlineData("Bearer " + Key)]
        [InlineData("I-API-KEY " + Key)]
        [InlineData("i-api-key wrong")]
        [InlineData("i-api-key " + Key + "0")]
        [InlineData("i-api-key c0ffee00-1111-2222-3333-44445555666")]
        [InlineData("i-api-key " + Key + " trailing")]
        public async Task EverythingButTheCorrectKey_IsRefusedWithTheSameAnswer(string? authorization)
        {
            var calls = new ProbeCalls();
            using var host = await StartAsync(Key, calls);

            var (status, body) = await GetAsync(host, "/internal/hardening-probe", authorization);

            status.Should().Be((int)HttpStatusCode.Unauthorized);
            body.Should().Be(Unauthorized);
            calls.Count.Should().Be(0);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task ABlankConfiguredKey_DoesNotAdmitABlankPresentedKey(string? configuredKey)
        {
            var calls = new ProbeCalls();
            using var host = await StartAsync(configuredKey, calls);

            var (status, body) = await GetAsync(host, "/internal/hardening-probe", "i-api-key ");

            status.Should().Be((int)HttpStatusCode.Unauthorized);
            body.Should().Be(Unauthorized);
            calls.Count.Should().Be(0);
        }

        [Theory]
        [InlineData(null, "i-api-key anything")]
        [InlineData("", "i-api-key anything")]
        [InlineData(" ", "i-api-key anything")]
        [InlineData(" ", "i-api-key  ")]
        [InlineData("\t", "i-api-key \t")]
        public async Task ABlankConfiguredKey_AdmitsNoHeaderAtAll(string? configuredKey, string authorization)
        {
            var calls = new ProbeCalls();
            using var host = await StartAsync(configuredKey, calls);

            var (status, _) = await GetAsync(host, "/internal/hardening-probe", authorization);

            status.Should().Be((int)HttpStatusCode.Unauthorized);
            calls.Count.Should().Be(0);
        }

        private static Task<IHost> StartOxQLAsync(RecordingQueryService service) =>
            HardeningTestHost.StartAsync([typeof(OxQLInternalController)], services =>
            {
                services.AddSingleton<IOxQLQueryService>(service);
                services.AddSingleton(new OxQLOptions());
                services.Configure<AuthSettings>(settings => settings.InternalApiKey = Key);
            });

        private static async Task<int> PostAsync(IHost host, string path, string body, string? authorization)
        {
            var context = await host.GetTestServer().SendAsync(request =>
            {
                request.Request.Method = HttpMethods.Post;
                request.Request.Path = path;
                request.Request.ContentType = "application/json";
                request.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

                if (authorization is not null)
                    request.Request.Headers.Authorization = authorization;
            });

            return context.Response.StatusCode;
        }

        private const string ExplainBody = """{"checks":[{"query":{"entityType":"vehicle.vehicle","pipeline":[]},"catalog":[{"id":"c1","entity":"vehicle.vehicle"}],"remote":"check"}],"budget":{"ms":750,"calls":3}}""";

        private const string KeyedBatchBody = """{"queries":[{"entityType":"vehicle.vehicle","keyedBy":{"path":"id","keys":["c0ffee00-1111-2222-3333-444455556666"],"perKey":2},"pipeline":[]}],"maxTimeMs":250}""";

        [Theory]
        [InlineData(null)]
        [InlineData("Bearer " + Key)]
        [InlineData("i-api-key wrong")]
        public async Task TheInternalExplain_WithoutTheKey_IsRefusedWithoutExplaining(string? authorization)
        {
            var service = new RecordingQueryService();
            using var host = await StartOxQLAsync(service);

            (await PostAsync(host, "/internal/oxql/explain", ExplainBody, authorization)).Should().Be((int)HttpStatusCode.Unauthorized);
            service.ExplainBatches.Should().BeEmpty();
        }

        [Fact]
        public async Task TheInternalExplain_WithTheKey_ExplainsTheChecksAsOneInternalCall()
        {
            var service = new RecordingQueryService();
            using var host = await StartOxQLAsync(service);

            (await PostAsync(host, "/internal/oxql/explain", ExplainBody, $"i-api-key {Key}")).Should().Be((int)HttpStatusCode.OK);

            var batch = service.ExplainBatches.Should().ContainSingle().Subject;
            var request = batch.Checks.Should().ContainSingle().Subject;
            request.IsEnvelope.Should().BeTrue();
            request.Remote.Should().Be(ExplainRequest.RemoteCheck);
            request.Catalog.Should().ContainSingle().Which["id"]!.GetValue<string>().Should().Be("c1");

            // What the origin has left rides in the body, for all the checks together: the owner never does more than that.
            batch.Budget!.Ms.Should().Be(750);
            batch.Budget.Calls.Should().Be(3);
            service.Explains.Should().BeEmpty("the checks are explained through the batch, which alone shares their owner calls");
        }

        [Fact]
        public async Task TheInternalBatch_WithTheKey_HandsAKeyedFetchToTheServiceAsAnInternalCall()
        {
            var service = new RecordingQueryService();
            using var host = await StartOxQLAsync(service);

            (await PostAsync(host, "/internal/oxql/batch", KeyedBatchBody, $"i-api-key {Key}")).Should().Be((int)HttpStatusCode.OK);

            var (batch, internalCall) = service.Batches.Should().ContainSingle().Subject;
            internalCall.Should().BeTrue();
            batch.MaxTimeMs.Should().Be(250);
            batch.Queries.Should().ContainSingle().Which.KeyedBy.Should().NotBeNull();
        }

        [Theory]
        [InlineData("/internal/oxql/explain", "{")]
        [InlineData("/internal/oxql/batch", "{\"queries\":7}")]
        public async Task AnInternalRoute_WithoutTheKey_IsRefusedBeforeItsBodyIsJudged(string path, string body)
        {
            var service = new RecordingQueryService();
            using var host = await StartOxQLAsync(service);

            (await PostAsync(host, path, body, null)).Should().Be((int)HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task TheInternalBatch_WithoutTheKey_IsRefusedWithoutExecuting()
        {
            var service = new RecordingQueryService();
            using var host = await StartOxQLAsync(service);

            (await PostAsync(host, "/internal/oxql/batch", KeyedBatchBody, null)).Should().Be((int)HttpStatusCode.Unauthorized);
            service.Batches.Should().BeEmpty();
        }

        [Fact]
        public async Task TheCorrectKey_OnAControllerThatIsNotInternal_IsRefusedWithoutRunningTheAction()
        {
            var calls = new ProbeCalls();
            using var host = await StartAsync(Key, calls);

            var (status, body) = await GetAsync(host, "/hardening-plain-probe", $"i-api-key {Key}");

            status.Should().Be((int)HttpStatusCode.BadRequest);
            body.Should().StartWith("Internal calls are only allowed for OxSInternalController.");
            calls.Count.Should().Be(0);
        }
    }

    /// <summary>How often a probe action ran.</summary>
    public sealed class ProbeCalls
    {
        private int count;

        /// <summary>The number of executed probe actions.</summary>
        public int Count => count;

        /// <summary>Records one executed action.</summary>
        public void Record() => Interlocked.Increment(ref count);
    }

    /// <summary>A query service that keeps every batch and explain it was handed, with the internal-call flag, and answers empty.</summary>
    public sealed class RecordingQueryService : IOxQLQueryService
    {
        /// <summary>Every batch, in order.</summary>
        public List<(BatchRequest Batch, bool InternalCall)> Batches { get; } = [];

        /// <summary>Every explain, in order.</summary>
        public List<(ExplainRequest Request, bool InternalCall)> Explains { get; } = [];

        /// <summary>Every internal explain batch, in order.</summary>
        public List<ExplainBatchRequest> ExplainBatches { get; } = [];

        /// <inheritdoc/>
        public Task<ExplainBatchOutcome> ExplainBatchAsync(ExplainBatchRequest batch, CancellationToken cancellationToken = default)
        {
            ExplainBatches.Add(batch);

            return Task.FromResult<ExplainBatchOutcome>(new ExplainBatchOutcome.Success(new ExplainBatchResponse { Answers = [.. batch.Checks.Select(_ => (JsonNode?)new JsonObject())] }));
        }

        /// <inheritdoc/>
        public Task<QueryOutcome> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The internal routes never execute a single query.");

        /// <inheritdoc/>
        public Task<QueryOutcome> ExecuteAsync(QueryRequest request, int? maxTimeMs, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The internal routes never execute a single query.");

        /// <inheritdoc/>
        public Task<BatchOutcome> BatchAsync(BatchRequest batch, CancellationToken cancellationToken = default) =>
            BatchAsync(batch, false, cancellationToken);

        /// <inheritdoc/>
        public Task<BatchOutcome> BatchAsync(BatchRequest batch, bool internalCall, CancellationToken cancellationToken = default)
        {
            Batches.Add((batch, internalCall));

            return Task.FromResult<BatchOutcome>(new BatchOutcome.Success(new BatchResponse { Results = [.. batch.Queries.Select(_ => (JsonNode?)new JsonObject())] }));
        }

        /// <inheritdoc/>
        public Task<ExplainOutcome> ExplainAsync(ExplainRequest request, CancellationToken cancellationToken = default) =>
            ExplainAsync(request, false, cancellationToken);

        /// <inheritdoc/>
        public Task<ExplainOutcome> ExplainAsync(ExplainRequest request, bool internalCall, CancellationToken cancellationToken = default)
        {
            Explains.Add((request, internalCall));

            return Task.FromResult<ExplainOutcome>(new ExplainOutcome.Success(ExplainResult.Invalid(EngineCapabilities.Contract, new ExplainEngine { Capabilities = [] }, [])));
        }
    }

    /// <summary>An internal controller as a service writes one.</summary>
    [ApiController]
    [Route("internal/hardening-probe")]
    public sealed class HardeningInternalProbeController(ProbeCalls calls) : OxSInternalController
    {
        /// <summary>Answers once the guard admitted the call.</summary>
        [HttpGet]
        public IActionResult Get(CancellationToken ct)
        {
            calls.Record();

            return Content("reached");
        }
    }

    /// <summary>A controller that carries the guard without deriving from the internal base class.</summary>
    [ApiController]
    [AuthorizeInternalApiKey]
    [Route("hardening-plain-probe")]
    public sealed class HardeningPlainProbeController(ProbeCalls calls) : ControllerBase
    {
        /// <summary>Must never run: the guard refuses this controller.</summary>
        [HttpGet]
        public IActionResult Get(CancellationToken ct)
        {
            calls.Record();

            return Content("reached");
        }
    }
}

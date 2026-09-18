using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OxQL.Core.Models;
using OxQL.AspNetCore.Scope;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.Services;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>The owner's batch route: where it is, what travels on the call, and how a failure surfaces.</summary>
    public sealed class RemoteQueryClientTests
    {
        private const string Key = "0e46ea43-6b5e-4b31-8008-95df146cf97d";
        private static readonly Guid User = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid Organisation = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Correlation = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];
            public List<string> Bodies { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));

                return answer(request);
            }
        }

        private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public string? Named { get; private set; }

            public HttpClient CreateClient(string name)
            {
                Named = name;
                return new HttpClient(handler, disposeHandler: false);
            }
        }

        private static IConfiguration Configuration(params (string Key, string Value)[] pairs) =>
            new ConfigurationBuilder().AddInMemoryCollection(pairs.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)).Build();

        private static IHttpContextAccessor Accessor(IRequestContext? context)
        {
            if (context is null)
                return new HttpContextAccessor();

            // The same wiring the host has: the scope provider over the request context. The
            // client forwards what the provider answers, so the owner is asked under the very
            // organisation the engine scoped the parent query with.
            var services = new ServiceCollection();
            services.AddSingleton(context);
            services.AddSingleton<IOxQLScopeProvider, OxQLScopeProvider>();

            return new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() } };
        }

        private static RemoteQueryClient Client(IHttpClientFactory factory, IConfiguration configuration, IRequestContext? context = null) =>
            new(factory, Options.Create(new AuthSettings { InternalApiKey = Key }), configuration, Accessor(context), NullLogger<RemoteQueryClient>.Instance);

        [Fact]
        public void Urls_FollowTheInternalConventionWithThePerServiceVersion()
        {
            var client = Client(new Factory(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), Configuration(
                ("InternalHosts:vehicle", "localhost:8080"),
                ("InternalHosts:erp", "simplic_erp"),
                ("InternalApiVersions:vehicle", "v2")));

            client.IsConfigured("vehicle").Should().BeTrue();
            client.IsConfigured("erp").Should().BeTrue();
            client.IsConfigured("hr").Should().BeFalse();
            client.BatchUrl("vehicle").Should().Be("http://localhost:8080/vehicle-api/v2/internal/oxql/batch");
            client.BatchUrl("erp").Should().Be("http://simplic_erp/erp-api/v1/internal/oxql/batch", "a service without a version entry is v1");
            client.HealthUrl("vehicle").Should().Be("http://localhost:8080/vehicle-api/v2/OxQL/health");
            client.BatchUrl("hr").Should().BeNull();
        }

        [Fact]
        public async Task Batch_PostsTheRequestWithTheKeyTheContextAndTheContractHeader()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"results":[{"items":[{"id":"x"}],"pageInfo":{"hasNextPage":false}}]}""", System.Text.Encoding.UTF8, "application/json"),
            });
            var factory = new Factory(handler);
            var client = Client(factory, Configuration(("InternalHosts:vehicle", "localhost:8080"), ("InternalApiVersions:vehicle", "v2")),
                new RequestContext { UserId = User, OrganizationId = Organisation, CorrelationId = Correlation });

            var request = new BatchRequest
            {
                Queries = [new QueryRequest { EntityType = "vehicle.vehicle", Pipeline = [] }],
                MaxTimeMs = 1500,
            };

            var response = await client.BatchAsync("vehicle", request, TimeSpan.FromSeconds(2), CancellationToken.None);

            factory.Named.Should().Be(RemoteQueryClient.HttpClientName);

            var sent = handler.Requests.Should().ContainSingle().Subject;
            sent.Method.Should().Be(HttpMethod.Post);
            sent.RequestUri!.ToString().Should().Be("http://localhost:8080/vehicle-api/v2/internal/oxql/batch");
            sent.Headers.Authorization!.Scheme.Should().Be("i-api-key");
            sent.Headers.Authorization.Parameter.Should().Be(Key);
            sent.Headers.GetValues("X-OxQL-Contract").Should().Equal("2");
            sent.Headers.GetValues("UserId").Should().Equal(User.ToString());
            sent.Headers.GetValues("OrganizationId").Should().Equal(Organisation.ToString());
            sent.Headers.GetValues("X-Correlation-ID").Should().Equal(Correlation.ToString());

            var body = JsonNode.Parse(handler.Bodies[0])!.AsObject();
            body["queries"]!.AsArray().Should().HaveCount(1);
            body["queries"]![0]!["entityType"]!.GetValue<string>().Should().Be("vehicle.vehicle");
            body["maxTimeMs"]!.GetValue<int>().Should().Be(1500);

            response.Results.Should().ContainSingle();
            response.Results[0]!["items"]!.AsArray().Should().HaveCount(1);
        }

        [Fact]
        public async Task Batch_WithoutARequestContext_SendsNoContextHeaders()
        {
            var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"results":[]}""", System.Text.Encoding.UTF8, "application/json"),
            });
            var client = Client(new Factory(handler), Configuration(("InternalHosts:vehicle", "localhost:8080")));

            await client.BatchAsync("vehicle", new BatchRequest { Queries = [] }, TimeSpan.Zero, CancellationToken.None);

            var sent = handler.Requests.Single();
            sent.Headers.Contains("UserId").Should().BeFalse();
            sent.Headers.Contains("OrganizationId").Should().BeFalse();
            sent.Headers.Authorization!.Scheme.Should().Be("i-api-key");
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task Batch_ThrowsOnANonSuccessAnswer(HttpStatusCode status)
        {
            var client = Client(new Factory(new RecordingHandler(_ => new HttpResponseMessage(status))), Configuration(("InternalHosts:vehicle", "localhost:8080")));

            var call = () => client.BatchAsync("vehicle", new BatchRequest { Queries = [] }, TimeSpan.Zero, CancellationToken.None);

            (await call.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(status);
        }

        [Fact]
        public async Task Batch_ToAnUnconfiguredService_Throws()
        {
            var client = Client(new Factory(new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), Configuration());

            var call = () => client.BatchAsync("vehicle", new BatchRequest { Queries = [] }, TimeSpan.Zero, CancellationToken.None);

            await call.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Reachable_IsTheHealthRouteAnsweringSuccess()
        {
            var handler = new RecordingHandler(request => new HttpResponseMessage(request.RequestUri!.AbsolutePath.EndsWith("/OxQL/health") ? HttpStatusCode.OK : HttpStatusCode.NotFound));
            var client = Client(new Factory(handler), Configuration(("InternalHosts:vehicle", "localhost:8080"), ("InternalHosts:erp", "simplic_erp")));

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeTrue();
            (await client.IsReachableAsync("hr", CancellationToken.None)).Should().BeFalse("hr is not configured");
            handler.Requests.Single().Method.Should().Be(HttpMethod.Get);
        }

        [Fact]
        public async Task Reachable_IsFalseWhenTheOwnerThrows()
        {
            var client = Client(new Factory(new RecordingHandler(_ => throw new HttpRequestException("down"))), Configuration(("InternalHosts:vehicle", "localhost:8080")));

            (await client.IsReachableAsync("vehicle", CancellationToken.None)).Should().BeFalse();
        }
    }
}

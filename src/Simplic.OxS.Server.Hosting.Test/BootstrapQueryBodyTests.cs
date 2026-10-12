using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Simplic.OxS.Server.Hosting.Test.Fixtures;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// What a host built through <see cref="Bootstrap"/> answers to a query body it cannot bind.
    /// </summary>
    public sealed class BootstrapQueryBodyTests
    {
        /// <summary>A query whose pipeline is an object where the stages belong.</summary>
        private const string Unbindable = """{"entityType":"parcel.parcel","pipeline":{}}""";

        private static Task<IHost> StartAsync(AnsweringEngine engine) =>
            BootstrapHost.StartAsync<ParcelStartup>(engine.Register);

        private static HttpRequestMessage Post(string path, string body, int? contract)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

            if (contract is { } number)
                message.Headers.Add("X-OxQL-Contract", number.ToString());

            return message;
        }

        private static HttpRequestMessage AsUser(HttpRequestMessage message)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Callers.Bearer());

            return message;
        }

        [Theory]
        [InlineData(null)]
        [InlineData(1)]
        public async Task AContract1BodyThePublicRouteCannotBind_IsAnsweredByTheHostAsItAlwaysWas(int? contract)
        {
            var engine = new AnsweringEngine();

            using var host = await StartAsync(engine);
            using var client = host.GetTestClient();
            using var answer = await client.SendAsync(AsUser(Post("/parcel-api/v1/OxQL/query", Unbindable, contract)));

            var text = await answer.Content.ReadAsStringAsync();

            answer.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);
            answer.Content.Headers.ContentType!.ToString().Should().Be("application/problem+json");

            // The host's own problem document: its members, and one list of sentences per place the binding failed at.
            var body = JsonNode.Parse(text)!.AsObject();

            body.Select(member => member.Key).Should().Equal("type", "title", "status", "detail", "instance", "traceId", "errors");
            body["type"]!.GetValue<string>().Should().Be("urn:simplic-oxs:problem:bad-request");
            body["title"]!.GetValue<string>().Should().Be("Bad Request");
            body["status"]!.GetValue<int>().Should().Be(400);
            body["detail"]!.GetValue<string>().Should().Be("One or more validation errors occurred.");
            body["instance"]!.GetValue<string>().Should().Be("/OxQL/query");
            body["errors"]!.AsObject().Select(place => place.Key).Should().Equal("request", "$.pipeline");
            body["errors"]!.AsObject().ToList().Should().OnlyContain(place => place.Value!.AsArray().Count == 1 && place.Value[0]!.GetValue<string>().Length > 0);

            // Not the query engine's envelope, which a contract 1 caller never read here.
            body.ContainsKey("code").Should().BeFalse();
            answer.Headers.Contains("Server-Timing").Should().BeFalse();

            engine.Executed.Should().BeEmpty();
        }

        private static HttpRequestMessage AsService(HttpRequestMessage message)
        {
            message.Headers.TryAddWithoutValidation("Authorization", $"i-api-key {BootstrapHost.InternalApiKey}");
            message.Headers.Add("OrganizationId", Callers.Organisation.ToString());
            message.Headers.Add("UserId", Callers.User.ToString());

            return message;
        }

        [Fact]
        public async Task AnEntryOfAnInternalBatchTheHostCannotBind_IsRefusedInItsPlaceAndTheOthersRun()
        {
            var engine = new AnsweringEngine();

            using var host = await StartAsync(engine);
            using var client = host.GetTestClient();

            var batch = """{"queries":[{"entityType":"parcel.parcel","pipeline":[]},""" + Unbindable + """,{"entityType":"parcel.parcel","pipeline":[{"page":{"limit":1}}]}],"maxTimeMs":2000}""";

            using var answer = await client.SendAsync(AsService(Post("/parcel-api/v1/internal/oxql/batch", batch, contract: 2)));

            var text = await answer.Content.ReadAsStringAsync();

            answer.StatusCode.Should().Be(HttpStatusCode.OK, text);

            var results = JsonNode.Parse(text)!["results"]!.AsArray();

            results.Should().HaveCount(3);
            results[0]!["items"]!.AsArray().Should().ContainSingle();
            results[2]!["items"]!.AsArray().Should().ContainSingle();

            var refused = results[1]!.AsObject();

            refused.ContainsKey("items").Should().BeFalse();
            refused["type"]!.GetValue<string>().Should().Be("validation_error");

            var error = refused["errors"]!.AsArray().Should().ContainSingle().Subject!;

            error["code"]!.GetValue<string>().Should().Be("INVALID_REQUEST_BODY");
            error["params"]!["path"]!.GetValue<string>().Should().Be("$.pipeline");
            error["params"]!["entry"]!.GetValue<int>().Should().Be(1);
            error["message"]!.GetValue<string>().Should().NotContain("System.", "the refusal is said in the terms of the wire");

            engine.Executed.Should().HaveCount(2);
        }

        [Theory]
        [InlineData("""{"queries":{}}""")]
        [InlineData("""{"queries":[""")]
        public async Task AnInternalBatchTheHostCannotBindAtAll_IsRefusedInTheEnvelope(string batch)
        {
            var engine = new AnsweringEngine();

            using var host = await StartAsync(engine);
            using var client = host.GetTestClient();
            using var answer = await client.SendAsync(AsService(Post("/parcel-api/v1/internal/oxql/batch", batch, contract: 2)));

            var text = await answer.Content.ReadAsStringAsync();

            answer.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);
            answer.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

            var body = JsonNode.Parse(text)!;

            body["type"]!.GetValue<string>().Should().Be("validation_error");
            body["errors"]![0]!["code"]!.GetValue<string>().Should().Be("INVALID_REQUEST_BODY");
            body["errors"]![0]!["params"]!["reason"]!.GetValue<string>().Should().NotBeNullOrEmpty();
            text.Should().NotContain("System.");
            engine.Executed.Should().BeEmpty();
        }

        [Fact]
        public async Task AnInternalBatchWithoutTheKey_IsRefusedBeforeItsBodyIsLookedAt()
        {
            var engine = new AnsweringEngine();

            using var host = await StartAsync(engine);
            using var client = host.GetTestClient();
            using var answer = await client.SendAsync(Post("/parcel-api/v1/internal/oxql/batch", """{"queries":{}}""", contract: 2));

            answer.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await answer.Content.ReadAsStringAsync()).Should().NotContain("INVALID_REQUEST_BODY");
            engine.Executed.Should().BeEmpty();
        }

        [Fact]
        public async Task AContract2BodyThePublicRouteCannotBind_IsRefusedInTheEnvelope()
        {
            var engine = new AnsweringEngine();

            using var host = await StartAsync(engine);
            using var client = host.GetTestClient();
            using var answer = await client.SendAsync(AsUser(Post("/parcel-api/v1/OxQL/query", Unbindable, contract: 2)));

            var text = await answer.Content.ReadAsStringAsync();

            answer.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);

            var body = JsonNode.Parse(text)!;

            body["type"]!.GetValue<string>().Should().Be("validation_error");
            body["errors"]![0]!["code"]!.GetValue<string>().Should().Be("INVALID_REQUEST_BODY");
            body["errors"]![0]!["params"]!["path"]!.GetValue<string>().Should().Be("$.pipeline");
            engine.Executed.Should().BeEmpty();
        }
    }
}

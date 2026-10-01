using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using OxQL.Core.Binding;
using OxQL.Core.Cursor;
using OxQL.Core.Engine;
using OxQL.Core.Models;
using OxQL.Model;
using OxQL.Model.Addon;
using OxQL.Model.Attributes;
using OxQL.Model.Build;
using OxQL.Mongo;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>
    /// The query engine's explain over this host's remote client: an owner's route names the api
    /// version this host routes it to (<c>InternalApiVersions</c>), which the engine reads through
    /// <see cref="IRemoteOwnerInfo.ApiVersionOf"/>.
    /// </summary>
    public sealed class ExplainOwnerRouteTests
    {
        /// <summary>Explain never executes, so a runner that is called is a failure.</summary>
        private sealed class NoRunner : IAggregateRunner
        {
            public Task<IReadOnlyList<BsonDocument>> AggregateAsync(EntityDef entity, IReadOnlyList<BsonDocument> stages, AggregateRunOptions options, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("Explain must not execute.");
        }

        /// <summary>An owner that answers every call with an empty, valid answer.</summary>
        private sealed class OwnerHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"valid":true,"contract":2}""", Encoding.UTF8, "application/json") });
        }

        private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
        }

        /// <summary>An organisation-scoped entity of this host whose member names an entity of another service.</summary>
        private sealed class ScopedContact
        {
            public Guid Id { get; set; }

            public Guid OrganizationId { get; set; }

            [OxQLReference("staff.employee", "id")]
            public Guid EmployeeId { get; set; }
        }

        [Fact]
        public async Task Explain_OfAResolveIntoAnotherService_NamesTheApiVersionThisHostRoutesTheOwnerTo()
        {
            var models = new StaticEntityModelProvider(ClrModelBuilder.Build(
                [new EntityDeclaration("probe.contact", "probe.contact", typeof(ScopedContact), "contacts", null, false)]));
            var remote = new RemoteQueryClient(
                new Factory(new OwnerHandler()),
                Options.Create(new AuthSettings { InternalApiKey = "key" }),
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["InternalHosts:staff"] = "staff-svc:8080",
                    ["InternalApiVersions:staff"] = "v2",
                }).Build(),
                new HttpContextAccessor(),
                NullLogger<RemoteQueryClient>.Instance);
            var options = new OxQLOptions { Cursor = { SigningKey = "test-signing-key" } };
            var engine = new MongoQueryEngine(models, new NoRunner(), new CursorCodec("test-signing-key"), options, remote);

            ((IRemoteOwnerInfo)remote).ApiVersionOf("staff").Should().Be("v2", "the engine reads the version through its interface");

            var request = System.Text.Json.JsonSerializer.Deserialize<QueryRequest>(
                """{ "entityType": "probe.contact", "pipeline": [ { "resolve": { "path": "employeeId", "as": "employee", "select": ["name"] } } ] }""",
                OxQLJson.Wire)!;
            var context = new RequestContext
            {
                Organisation = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Options = options,
                AddonSource = EmptyAddonDefinitionSource.Instance,
                Contract = 2,
            };

            var explained = await engine.ExplainAsync(request, context);

            var result = explained.Should().BeOfType<ExplainOutcome.Success>(explained is ExplainOutcome.Refused refused ? string.Join("; ", refused.Refusal.Errors?.Select(error => $"{error.Code}: {error.Message}") ?? [refused.Refusal.Title]) : "").Subject.Result;
            var owner = result.Owners.Should().ContainSingle().Subject;

            owner["route"]!.ToJsonString().Should().Be("""{"apiName":"staff-api","apiVersion":"v2"}""");
        }
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OxQL.AspNetCore.OpenApi;
using OxQL.Core.Models;
using Simplic.OxS.Server.Hosting.Test.Fixtures;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// What the OpenAPI document of a service says of the query engine's routes: the operations,
    /// their parameters, statuses and models, the security of each, one example of every body, and
    /// nothing of the routes and members only services use among themselves. A client generated
    /// from the document is a client of contract 2; these tests hold the document to that.
    /// </summary>
    public sealed class OpenApiQueryRouteTests
    {
        private const string Json = "application/json";

        private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

        private static async Task<JsonObject> DocumentAsync(string environment = "Production")
        {
            using var host = await BootstrapHost.StartAsync<ParcelStartup>(environment: environment);

            return await OpenApiDocumentTests.DocumentAsync(host);
        }

        private static string[] Keys(JsonNode? node) => OpenApiDocumentTests.Keys(node);

        private static string? Reference(JsonNode? schema) => OpenApiDocumentTests.Reference(schema);

        private static string[] Parameters(JsonNode operation) =>
            [.. (operation["parameters"]?.AsArray() ?? []).Select(parameter => $"{parameter!["in"]!.GetValue<string>()} {parameter["name"]!.GetValue<string>()}")];

        private static JsonNode Parameter(JsonNode operation, string name) =>
            operation["parameters"]!.AsArray().Single(parameter => parameter!["name"]!.GetValue<string>() == name)!;

        /// <summary>The media types of a status with the schema each names, as <c>type:Schema</c>; empty for a status without a body.</summary>
        private static string[] Bodies(JsonNode operation, string status) =>
            operation["responses"]![status]!["content"] is JsonObject content
                ? [.. content.Select(media => $"{media.Key}:{Reference(media.Value!["schema"])}")]
                : [];

        [Theory]
        [InlineData("Production")]
        [InlineData("Development")]
        public async Task TheDocument_ListsTheQueryAndSchemaRoutesBesideTheServicesOwn_AndNoRouteBetweenServices(string environment)
        {
            var document = await DocumentAsync(environment);

            Keys(document["paths"]).Should().BeEquivalentTo(
                ["/OxQL/query", "/OxQL/batch", "/OxQL/explain", "/OxQL/health", "/schema", "/schema/addons", "/Parcel", "/Parcel/{id}", "/Parcel/open"]);

            Keys(document["paths"]!["/OxQL/query"]).Should().Equal("post");
            Keys(document["paths"]!["/OxQL/batch"]).Should().Equal("post");
            Keys(document["paths"]!["/OxQL/explain"]).Should().Equal("post");
            Keys(document["paths"]!["/OxQL/health"]).Should().Equal("get");

            // What only the route between services takes or answers is nowhere in the document.
            var text = document.ToJsonString();

            text.Should().NotContain("keyedBy").And.NotContain("ExplainBatch").And.NotContain("internal/oxql");
        }

        public static TheoryData<string, string, string, string, string?, string, string[], string[]> Described => new()
        {
            // path, method, summary, description, body model, answer model, parameters, statuses
            {
                "/OxQL/query", "post", OxQLDocumentation.QuerySummary, OxQLDocumentation.QueryDescription, nameof(OxQLQueryBody), nameof(OxQLQueryAnswer),
                ["header X-OxQL-Contract", "header X-OxQL-Timing"], ["200", "400", "401", "403", "413", "422", "500", "504"]
            },
            {
                "/OxQL/batch", "post", OxQLDocumentation.BatchSummary, OxQLDocumentation.BatchDescription, nameof(OxQLBatchBody), nameof(OxQLBatchAnswer),
                ["header X-OxQL-Contract", "header X-OxQL-Timing"], ["200", "400", "401", "413", "500"]
            },
            {
                "/OxQL/explain", "post", OxQLDocumentation.ExplainSummary, OxQLDocumentation.ExplainDescription, nameof(OxQLExplainBody), nameof(OxQLExplainAnswer),
                ["header X-OxQL-Contract", "header If-None-Match"], ["200", "304", "400", "401", "403", "404", "413", "429", "500"]
            },
            {
                "/OxQL/health", "get", OxQLDocumentation.HealthSummary, OxQLDocumentation.HealthDescription, null, nameof(OxQLHealthAnswer),
                ["query shallow", "header X-OxQL-Contract"], ["200"]
            },
        };

        [Theory]
        [MemberData(nameof(Described))]
        public async Task EveryQueryRoute_HasItsSummaryDescriptionParametersModelsAndStatuses(
            string path, string method, string summary, string description, string? body, string answer, string[] parameters, string[] statuses)
        {
            var document = await DocumentAsync();
            var operation = document["paths"]![path]![method]!;

            // What the engine says of the route, and a pointer to the documentation in place of the language.
            operation["summary"]!.GetValue<string>().Should().Be(summary);
            operation["description"]!.GetValue<string>().Should().StartWith(description).And.Contain(OxQLDocumentation.Url);

            foreach (var ofTheLanguage in new[] { "startsWith", "$var", "dateTrunc", "\"eq\"", "| " })
                operation["description"]!.GetValue<string>().Should().NotContain(ofTheLanguage, "the query language is documented in one place, and that is not this document");

            Parameters(operation).Should().BeEquivalentTo(parameters);

            // The contract header: optional, its two values, and described.
            var contract = Parameter(operation, "X-OxQL-Contract");

            contract["required"].Should().BeNull("a request without the header is contract 1");
            contract["schema"]!["type"]!.GetValue<string>().Should().Be("string");
            contract["schema"]!["enum"]!.ToJsonString().Should().Be("""["1","2"]""");
            contract["description"]!.GetValue<string>().Should().Contain("contract 1").And.Contain("`2`");

            // The body of the wire, as JSON, with an example.
            if (body is null)
            {
                operation["requestBody"].Should().BeNull();
            }
            else
            {
                var content = operation["requestBody"]!["content"]!;

                Keys(content).Should().Equal(Json);
                Reference(content[Json]!["schema"]).Should().Be(body);
                content[Json]!["example"].Should().NotBeNull();
            }

            // Every status the route answers and no other, each said in a sentence.
            Keys(operation["responses"]).Should().Equal(statuses);

            foreach (var status in statuses)
                operation["responses"]![status]!["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace().And.NotBe("Bad Request").And.NotBe("Unauthorized");

            Bodies(operation, "200").Should().Equal($"{Json}:{answer}");
            operation["responses"]!["200"]!["content"]![Json]!["example"].Should().NotBeNull();

            foreach (var status in statuses.Where(status => status is not ("200" or "304")))
            {
                // What the host answers before the engine sees the request is its problem document; everything else is the engine's envelope.
                Bodies(operation, status).Should().Equal([status is "401" or "404" ? "application/problem+json:ProblemDetails" : $"{Json}:{nameof(OxQLError)}"], status);
            }

            if (statuses.Contains("304"))
            {
                Bodies(operation, "304").Should().BeEmpty();
                operation["responses"]!["304"]!["headers"]!["ETag"].Should().NotBeNull();
                operation["responses"]!["200"]!["headers"]!["ETag"].Should().NotBeNull();
            }

            if (statuses.Contains("400"))
                operation["responses"]!["400"]!["content"]![Json]!["example"]!["errors"]![0]!["code"]!.GetValue<string>().Should().NotBeNullOrEmpty();

            if (statuses.Contains("429"))
                operation["responses"]!["429"]!["headers"]!["Retry-After"]!["schema"]!["type"]!.GetValue<string>().Should().Be("integer");
        }

        [Fact]
        public async Task TheAuthenticatedQueryRoutes_NeedWhatEveryRouteNeeds_AndHealthNeedsNothing()
        {
            var document = await DocumentAsync();
            var paths = document["paths"]!;

            document["security"]!.ToJsonString().Should().Be("""[{"Bearer":[],"ApiKey":[]}]""");

            // No requirement of their own: the document's applies, as to a service's own controllers.
            paths["/OxQL/query"]!["post"]!["security"].Should().BeNull();
            paths["/OxQL/batch"]!["post"]!["security"].Should().BeNull();
            paths["/OxQL/explain"]!["post"]!["security"].Should().BeNull();
            paths["/Parcel"]!["post"]!["security"].Should().BeNull();

            paths["/OxQL/health"]!["get"]!["security"]!.ToJsonString().Should().Be("[{}]");
        }

        [Fact]
        public async Task TheModels_AreTheOnesOfTheWire_WithoutTheEnginesOwnAndWithoutAnInternalMember()
        {
            var document = await DocumentAsync();
            var schemas = document["components"]!["schemas"]!.AsObject();

            Keys(schemas).Where(name => name.StartsWith("OxQL", StringComparison.Ordinal)).Should().BeEquivalentTo(
            [
                "OxQLQueryBody", "OxQLFilterEntry", "OxQLStage", "OxQLPageStage", "OxQLLookupStage", "OxQLResolveStage", "OxQLUnwindStage",
                "OxQLGroupStage", "OxQLGroupKey", "OxQLDateTrunc", "OxQLValuesStage", "OxQLBatchBody", "OxQLExplainBody", "OxQLExplainShape",
                "OxQLQueryAnswer", "OxQLPageInfo", "OxQLCount", "OxQLDiagnostic", "OxQLBatchAnswer", "OxQLError", "OxQLErrorDetail",
                "OxQLExplainAnswer", "OxQLHealthAnswer", "OxQLHealthEngine", "OxQLHealthService", "OxQLHealthModel",
                "OxQLContract1QueryBody", "OxQLContract1QueryAnswer", "OxQLContract1PageInfo",
            ]);

            // The types the engine binds a request into and writes an answer from are not what the routes publish.
            Keys(schemas).Should().NotContain(["QueryRequest", "PipelineStage", "PageStage", "KeyedByMember", "BatchRequest", "BatchResponse", "ExplainRequest", "Refusal", "QueryResult", "PageInfo"]);

            Keys(schemas["OxQLQueryBody"]!["properties"]).Should().BeEquivalentTo(["entityType", "pipeline", "variables", "filter", "strict", "onMissing", "timing", "maxTimeMs"]);
            schemas["OxQLQueryBody"]!["required"]!.AsArray().Select(name => name!.GetValue<string>()).Should().BeEquivalentTo(["entityType", "pipeline"]);
            Keys(schemas["OxQLStage"]!["properties"]).Should().BeEquivalentTo(PipelineStage.KnownKeys);
            Keys(schemas["OxQLPageStage"]!["properties"]).Should().BeEquivalentTo(["limit", "cursor", "count"]);
            Keys(schemas["OxQLPageInfo"]!["properties"]).Should().BeEquivalentTo(["hasNextPage", "nextCursor", "count"]);
            Keys(schemas["OxQLCount"]!["properties"]).Should().BeEquivalentTo(["state", "value", "stoppedBy", "reason", "ms"]);
            Keys(schemas["OxQLError"]!["properties"]).Should().BeEquivalentTo(["type", "title", "errors", "status"]);
            Keys(schemas["OxQLErrorDetail"]!["properties"]).Should().BeEquivalentTo(["code", "message", "stage", "filter", "path", "params", "basis"]);
            Keys(schemas["OxQLHealthModel"]!["properties"]).Should().BeEquivalentTo(["entities", "degraded", "reason"]);

            // The members are described by the comments the engine's package brings.
            schemas["OxQLQueryBody"]!["properties"]!["entityType"]!["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            schemas["OxQLPageStage"]!["properties"]!["count"]!["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Contract1_IsNamedAndNotOffered()
        {
            var document = await DocumentAsync();
            var schemas = document["components"]!["schemas"]!;
            var query = document["paths"]!["/OxQL/query"]!["post"]!;

            // Its models are in the document, marked as what they are.
            foreach (var legacy in new[] { "OxQLContract1QueryBody", "OxQLContract1QueryAnswer", "OxQLContract1PageInfo" })
                schemas[legacy]!["deprecated"]!.GetValue<bool>().Should().BeTrue(legacy);

            Keys(schemas["OxQLContract1PageInfo"]!["properties"]).Should().BeEquivalentTo(["hasNextPage", "nextCursor", "totalCount", "totalCountCapped"]);

            // The operation says where they are and stays the one of the current contract: no alternative a generated client would have to tell apart.
            query["description"]!.GetValue<string>().Should().Contain("`OxQLContract1QueryBody`").And.Contain("`OxQLContract1QueryAnswer`").And.Contain("deprecated");
            document["paths"]!.ToJsonString().Should().NotContain("#/components/schemas/OxQLContract1").And.NotContain("oneOf");

            foreach (var current in new[] { "OxQLQueryBody", "OxQLQueryAnswer", "OxQLPageInfo", "OxQLBatchBody", "OxQLError" })
                schemas[current]!["deprecated"].Should().BeNull(current);
        }

        [Fact]
        public async Task TheExamples_AreBodiesOfTheirModels_AndTheRequestsAreOnesTheEngineReads()
        {
            var document = await DocumentAsync();
            var paths = document["paths"]!;

            JsonNode Request(string path) => paths[path]!["post"]!["requestBody"]!["content"]![Json]!["example"]!;
            JsonNode Answer(string path, string method = "post", string status = "200") => paths[path]![method]!["responses"]![status]!["content"]![Json]!["example"]!;

            // Member for member the models of the wire: nothing a model does not have, nothing required left out.
            Request("/OxQL/query").Deserialize<OxQLQueryBody>(Strict)!.Pipeline.Should().HaveCount(3);
            Request("/OxQL/batch").Deserialize<OxQLBatchBody>(Strict)!.Queries.Should().HaveCount(2);
            Request("/OxQL/explain").Deserialize<OxQLExplainBody>(Strict)!.Query.EntityType.Should().Be("parcel.parcel");
            Answer("/OxQL/query").Deserialize<OxQLQueryAnswer>(Strict)!.PageInfo.Count.State.Should().Be("exact");
            Answer("/OxQL/batch").Deserialize<OxQLBatchAnswer>(Strict)!.Results.Should().HaveCount(2);
            Answer("/OxQL/explain").Deserialize<OxQLExplainAnswer>(Strict)!.Valid.Should().BeTrue();
            Answer("/OxQL/health", "get").Deserialize<OxQLHealthAnswer>(Strict)!.Model!.Degraded.Should().BeFalse();
            Answer("/OxQL/query", status: "400").Deserialize<OxQLError>(Strict)!.Errors.Should().ContainSingle();

            // An entry of a batch answer is a page or a refusal.
            var results = Answer("/OxQL/batch")["results"]!.AsArray();

            results[0].Deserialize<OxQLQueryAnswer>(Strict)!.Items.Should().ContainSingle();
            results[1].Deserialize<OxQLError>(Strict)!.Type.Should().Be("validation_error");

            // The engine's own reader takes the requests as they stand.
            var query = Request("/OxQL/query").Deserialize<QueryRequest>(OxQLJson.Wire)!;

            query.EntityType.Should().Be("parcel.parcel");
            query.Pipeline.Select(stage => stage.Kind).Should().Equal("match", "sort", "page");
            Request("/OxQL/batch").Deserialize<BatchRequest>(OxQLJson.Wire)!.Queries.Should().HaveCount(2);
            Request("/OxQL/explain").Deserialize<global::OxQL.Core.Engine.ExplainRequest>(OxQLJson.Wire)!.Query!.Pipeline.Should().HaveCount(2);

            // Short: an example shows a form, and no page of data.
            foreach (var example in new[] { Request("/OxQL/query"), Request("/OxQL/batch"), Request("/OxQL/explain"), Answer("/OxQL/query"), Answer("/OxQL/batch"), Answer("/OxQL/explain"), Answer("/OxQL/health", "get") })
                example.ToJsonString().Length.Should().BeLessThan(900);
        }
    }
}

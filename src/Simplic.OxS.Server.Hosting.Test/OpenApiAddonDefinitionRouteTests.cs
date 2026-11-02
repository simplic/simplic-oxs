using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OxQL.Core.Attributes;
using Simplic.OxS.Data;
using Simplic.OxS.Server.Controllers;
using Simplic.OxS.Server.Controllers.Model;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// What the OpenAPI document of a service says of the addon definition routes of the package
    /// (<c>/AddonDefinition</c>): the operations, their parameters, bodies and members with a
    /// description each, every status a route answers with its body and no other, and examples
    /// that are bodies of the models. The last test sends requests and holds the answers against
    /// what the document says of them.
    /// </summary>
    public sealed class OpenApiAddonDefinitionRouteTests
    {
        private const string Json = "application/json";
        private const string Text = "text/plain";
        private const string ProblemJson = "application/problem+json";

        private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

        private static async Task<JsonObject> DocumentAsync()
        {
            using var host = await BootstrapHost.StartAsync<CrateStartup>();

            return await OpenApiDocumentTests.DocumentAsync(host, "crate-api");
        }

        private static string[] Keys(JsonNode? node) => OpenApiDocumentTests.Keys(node);

        private static string? Reference(JsonNode? schema) => OpenApiDocumentTests.Reference(schema);

        public static TheoryData<string, string, string, string?, string?, string[], string[]> Operations => new()
        {
            // path, method, summary, body model, answer status and model, parameters, statuses
            { "/AddonDefinition/{entity}", "get", "The addon definitions of one entity.", null, "200 AddonDefinitionResponse[]", ["path entity"], ["200", "401", "404"] },
            { "/AddonDefinition/by-id/{id}", "get", "One addon definition.", null, "200 AddonDefinitionResponse", ["path id"], ["200", "401", "404"] },
            { "/AddonDefinition", "post", "Defines an addon property of an entity.", "CreateAddonDefinitionRequest", "201 AddonDefinitionResponse", [], ["201", "400", "401", "403", "409"] },
            { "/AddonDefinition/{id}", "put", "Changes the labels and the value list of an addon definition.", "UpdateAddonDefinitionRequest", "200 AddonDefinitionResponse", ["path id"], ["200", "400", "401", "404", "409"] },
            { "/AddonDefinition/{id}", "delete", "Retires an addon definition.", null, null, ["path id"], ["204", "401", "404", "409"] },
        };

        [Fact]
        public async Task TheDocument_HoldsTheFiveOperations_AndNoOther()
        {
            var paths = (await DocumentAsync())["paths"]!;

            Keys(paths).Where(path => path.StartsWith("/AddonDefinition", StringComparison.Ordinal))
                .Should().BeEquivalentTo("/AddonDefinition/{entity}", "/AddonDefinition/by-id/{id}", "/AddonDefinition", "/AddonDefinition/{id}");
            Keys(paths["/AddonDefinition/{entity}"]).Should().Equal("get");
            Keys(paths["/AddonDefinition/by-id/{id}"]).Should().Equal("get");
            Keys(paths["/AddonDefinition"]).Should().Equal("post");
            Keys(paths["/AddonDefinition/{id}"]).Should().BeEquivalentTo("put", "delete");
        }

        [Theory]
        [MemberData(nameof(Operations))]
        public async Task AnOperation_IsDescribedWithItsParametersItsBodiesAndEveryStatusItAnswers(
            string path, string method, string summary, string? body, string? answer, string[] parameters, string[] statuses)
        {
            var operation = (await DocumentAsync())["paths"]![path]![method]!;

            operation["summary"]!.GetValue<string>().Should().Be(summary);
            operation["description"]!.GetValue<string>().Should()
                .Contain("An addon definition is a property an organisation adds to an extendable entity")
                .And.Contain("https://github.com/simplic/simplic-oxs/blob/master/OX_SCHEMA.md");
            operation["security"].Should().BeNull("the route needs what the document requires of every route");

            // Every parameter, required as a part of the path is, and described.
            var declared = operation["parameters"]?.AsArray() ?? [];

            declared.Select(parameter => $"{parameter!["in"]!.GetValue<string>()} {parameter["name"]!.GetValue<string>()}").Should().Equal(parameters);

            foreach (var parameter in declared)
            {
                parameter!["required"]!.GetValue<bool>().Should().BeTrue();
                parameter["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            }

            // The body: JSON, its model, described, with an example.
            if (body is null)
            {
                operation["requestBody"].Should().BeNull();
            }
            else
            {
                var taken = operation["requestBody"]!;

                taken["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
                Keys(taken["content"]).Should().Equal(Json);
                Reference(taken["content"]![Json]!["schema"]).Should().Be(body);
                taken["content"]![Json]!["example"].Should().NotBeNull();
            }

            // Every status the route answers, each said in words, and no other.
            Keys(operation["responses"]).Should().Equal(statuses);

            foreach (var status in statuses)
            {
                var response = operation["responses"]![status]!;

                response["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace()
                    .And.NotBe("Bad Request").And.NotBe("Unauthorized").And.NotBe("Not Found").And.NotBe("Conflict");

                switch (status)
                {
                    case "200" or "201":
                        var written = response["content"]![Json]!;
                        var schema = written["schema"]!;

                        Keys(response["content"]).Should().Equal(Json);
                        $"{status} {(schema["type"]?.GetValue<string>() == "array" ? Reference(schema["items"]) + "[]" : Reference(schema))}".Should().Be(answer);
                        written["example"].Should().NotBeNull();
                        break;

                    case "204":
                        response["content"].Should().BeNull("a 204 has no body");
                        break;

                    case "401" or "403" or "404":
                        Keys(response["content"]).Should().Equal(ProblemJson);
                        Reference(response["content"]![ProblemJson]!["schema"]).Should().Be("ProblemDetails");
                        break;

                    case "400":
                        // The refusal of a rule is a sentence; a body the host cannot read is its problem document.
                        Keys(response["content"]).Should().Equal(Text, Json, ProblemJson);
                        response["content"]![Text]!["schema"]!["type"]!.GetValue<string>().Should().Be("string");
                        response["content"]![Json]!["schema"]!["type"]!.GetValue<string>().Should().Be("string");
                        Reference(response["content"]![ProblemJson]!["schema"]).Should().Be("ProblemDetails");
                        break;

                    case "409":
                        Keys(response["content"]).Should().Equal(Text, Json);
                        response["content"]![Text]!["schema"]!["type"]!.GetValue<string>().Should().Be("string");
                        response["content"]![Json]!["schema"]!["type"]!.GetValue<string>().Should().Be("string");
                        break;
                }
            }
        }

        [Theory]
        [InlineData("CreateAddonDefinitionRequest", "entity", "path", "kind", "values", "displayName", "description")]
        [InlineData("UpdateAddonDefinitionRequest", "values", "displayName", "description")]
        [InlineData("AddonDefinitionValueRequest", "value", "label")]
        [InlineData("AddonDefinitionResponse", "id", "entity", "path", "kind", "values", "displayName", "description", "retired")]
        [InlineData("AddonDefinitionValueResponse", "value", "label")]
        public async Task AModel_HasItsMembers_EachDescribed(string model, params string[] members)
        {
            var schema = (await DocumentAsync())["components"]!["schemas"]![model]!;

            Keys(schema["properties"]).Should().Equal(members);

            foreach (var member in members)
                schema["properties"]![member]!["description"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace($"{model}.{member} is described");
        }

        [Fact]
        public async Task TheValueLists_AreListsOfTheirValueModel()
        {
            var schemas = (await DocumentAsync())["components"]!["schemas"]!;

            Reference(schemas["CreateAddonDefinitionRequest"]!["properties"]!["values"]!["items"]).Should().Be("AddonDefinitionValueRequest");
            Reference(schemas["UpdateAddonDefinitionRequest"]!["properties"]!["values"]!["items"]).Should().Be("AddonDefinitionValueRequest");
            Reference(schemas["AddonDefinitionResponse"]!["properties"]!["values"]!["items"]).Should().Be("AddonDefinitionValueResponse");
            schemas["AddonDefinitionResponse"]!["properties"]!["id"]!["format"]!.GetValue<string>().Should().Be("uuid");
        }

        [Fact]
        public async Task TheExamples_AreBodiesOfTheModels()
        {
            var paths = (await DocumentAsync())["paths"]!;

            static JsonNode Request(JsonNode operation) => operation["requestBody"]!["content"]![Json]!["example"]!;
            static JsonNode Answer(JsonNode operation, string status) => operation["responses"]![status]!["content"]![Json]!["example"]!;

            // Every member is one of the model, and the body is one the route takes or answers.
            var create = Request(paths["/AddonDefinition"]!["post"]!).Deserialize<CreateAddonDefinitionRequest>(Strict)!;

            create.Entity.Should().Be("parcel.parcel");
            create.Path.Should().Be("handling");
            create.Kind.Should().Be("string");
            create.Values.Should().HaveCount(2);

            var created = Answer(paths["/AddonDefinition"]!["post"]!, "201").Deserialize<AddonDefinitionResponse>(Strict)!;

            created.Id.Should().NotBeEmpty();
            created.Path.Should().Be(create.Path);
            created.Retired.Should().BeFalse();

            var update = Request(paths["/AddonDefinition/{id}"]!["put"]!).Deserialize<UpdateAddonDefinitionRequest>(Strict)!;
            var updated = Answer(paths["/AddonDefinition/{id}"]!["put"]!, "200").Deserialize<AddonDefinitionResponse>(Strict)!;

            update.Values.Should().HaveCount(3);
            updated.Id.Should().Be(created.Id);
            updated.Values!.Select(value => value.Value).Should().Equal(update.Values!.Select(value => value.Value));

            Answer(paths["/AddonDefinition/by-id/{id}"]!["get"]!, "200").Deserialize<AddonDefinitionResponse>(Strict)!.Id.Should().Be(created.Id);

            var listed = Answer(paths["/AddonDefinition/{entity}"]!["get"]!, "200").Deserialize<List<AddonDefinitionResponse>>(Strict)!;

            listed.Select(definition => definition.Path).Should().BeInAscendingOrder(StringComparer.Ordinal).And.HaveCount(2);
            listed.Should().Contain(definition => definition.Retired);
        }

        [Fact]
        public async Task TheHost_AnswersTheStatusesWithTheBodiesTheDocumentNames()
        {
            using var host = await BootstrapHost.StartAsync<CrateStartup>(services =>
            {
                services.AddScoped(_ => DispatchProxy.Create<IAddonDefinitionRepository, NoDefinitions>());
                services.AddDistributedMemoryCache();
            });
            using var client = host.GetTestClient();

            async Task<(HttpStatusCode Status, string? Media, string Body)> SendAsync(HttpMethod method, string route, string? body = null, string? bearer = null, string? accept = null)
            {
                using var message = new HttpRequestMessage(method, route);

                if (body is not null)
                    message.Content = new StringContent(body, Encoding.UTF8, Json);

                if (bearer is not null)
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

                if (accept is not null)
                    message.Headers.Accept.ParseAdd(accept);

                using var answer = await client.SendAsync(message);

                return (answer.StatusCode, answer.Content.Headers.ContentType?.MediaType, await answer.Content.ReadAsStringAsync());
            }

            const string OfNoEntity = """{ "entity": "crate.lid", "path": "handling", "kind": "string" }""";
            var caller = Callers.Bearer();

            (await SendAsync(HttpMethod.Get, "/AddonDefinition/crate.crate")).Should().Match<(HttpStatusCode Status, string? Media, string Body)>(
                answer => answer.Status == HttpStatusCode.Unauthorized && answer.Media == ProblemJson);

            var none = await SendAsync(HttpMethod.Get, "/AddonDefinition/crate.crate", bearer: caller);

            none.Status.Should().Be(HttpStatusCode.OK);
            none.Media.Should().Be(Json);
            none.Body.Should().Be("[]");

            (await SendAsync(HttpMethod.Get, "/AddonDefinition/crate.lid", bearer: caller)).Should().Match<(HttpStatusCode Status, string? Media, string Body)>(
                answer => answer.Status == HttpStatusCode.NotFound && answer.Media == ProblemJson);

            (await SendAsync(HttpMethod.Delete, $"/AddonDefinition/{Guid.NewGuid()}", bearer: caller)).Should().Match<(HttpStatusCode Status, string? Media, string Body)>(
                answer => answer.Status == HttpStatusCode.NotFound && answer.Media == ProblemJson);

            // The refusal of a rule: a sentence, as text or as a JSON string, whichever the caller accepts.
            var refused = await SendAsync(HttpMethod.Post, "/AddonDefinition", OfNoEntity, caller);

            refused.Status.Should().Be(HttpStatusCode.BadRequest);
            refused.Media.Should().Be(Text);
            refused.Body.Should().Be("'crate.lid' is not an entity of this service.");

            var refusedAsJson = await SendAsync(HttpMethod.Post, "/AddonDefinition", OfNoEntity, caller, accept: Json);

            refusedAsJson.Media.Should().Be(Json);
            JsonNode.Parse(refusedAsJson.Body)!.GetValue<string>().Should().Be(refused.Body);

            // A body the host cannot read: its problem document.
            var unreadable = await SendAsync(HttpMethod.Post, "/AddonDefinition", """{ "entity": """, caller);

            unreadable.Status.Should().Be(HttpStatusCode.BadRequest);
            unreadable.Media.Should().Be(ProblemJson);
            JsonNode.Parse(unreadable.Body)!["status"]!.GetValue<int>().Should().Be(400);

            (await SendAsync(HttpMethod.Post, "/AddonDefinition", OfNoEntity, BearerWithoutOrganisation())).Should().Match<(HttpStatusCode Status, string? Media, string Body)>(
                answer => answer.Status == HttpStatusCode.Forbidden && answer.Media == ProblemJson);
        }

        [Fact]
        public async Task TheMembersADefinitionCannotBeStoredWithout_AreRequiredAndNotNullable_AndNoOtherIs()
        {
            var schemas = (await DocumentAsync())["components"]!["schemas"]!;

            void Requires(string model, string[] required, string[] optional)
            {
                var schema = schemas[model]!;

                (schema["required"]?.AsArray().Select(name => name!.GetValue<string>()) ?? []).Should().BeEquivalentTo(required, model);

                foreach (var member in required)
                    (schema["properties"]![member]!["nullable"]?.GetValue<bool>() ?? false).Should().BeFalse($"{model}.{member} is required");

                foreach (var member in optional)
                    schema["properties"]![member]!["nullable"]!.GetValue<bool>().Should().BeTrue($"{model}.{member} may be left out");

                Keys(schema["properties"]).Should().BeEquivalentTo([.. required, .. optional], model);
            }

            Requires("CreateAddonDefinitionRequest", ["entity", "path", "kind"], ["values", "displayName", "description"]);
            Requires("AddonDefinitionValueRequest", ["value"], ["label"]);
            Requires("UpdateAddonDefinitionRequest", [], ["values", "displayName", "description"]);
        }

        public static TheoryData<string, string?> BodiesWithoutARequiredMember => new()
        {
            // The body, and the sentence the route refuses it with; null where the host refuses it before the route, with its problem document.
            { """{ "path": "handling", "kind": "string" }""", "The entity id is required." },
            { """{ "entity": "", "path": "handling", "kind": "string" }""", "The entity id is required." },
            { """{ "entity": null, "path": "handling", "kind": "string" }""", null },
            { """{ "entity": "cask.cask", "kind": "string" }""", "The path is required." },
            { """{ "entity": "cask.cask", "path": "", "kind": "string" }""", "The path is required." },
            { """{ "entity": "cask.cask", "path": null, "kind": "string" }""", null },
            { """{ "entity": "cask.cask", "path": "handling" }""", "'' is not a kind; one of string, int, long, double, decimal, bool, date, dateTime, guid, object is." },
            { """{ "entity": "cask.cask", "path": "handling", "kind": "" }""", "'' is not a kind; one of string, int, long, double, decimal, bool, date, dateTime, guid, object is." },
            { """{ "entity": "cask.cask", "path": "handling", "kind": null }""", null },
            { """{ "entity": "cask.cask", "path": "handling", "kind": "string", "values": [ { "label": "Fragile" } ] }""", "Every value of the list must be non-empty." },
            { """{ "entity": "cask.cask", "path": "handling", "kind": "string", "values": [ { "value": "", "label": "Fragile" } ] }""", "Every value of the list must be non-empty." },
            { """{ "entity": "cask.cask", "path": "handling", "kind": "string", "values": [ { "value": null, "label": "Fragile" } ] }""", null },
        };

        private static async Task<(HttpStatusCode Status, string? Media, string Body)> CreateAsync(string body)
        {
            using var host = await BootstrapHost.StartAsync<CaskStartup>(services =>
            {
                services.AddScoped(_ => DispatchProxy.Create<IAddonDefinitionRepository, StoresEverything>());
                services.AddDistributedMemoryCache();
            });
            using var client = host.GetTestClient();
            using var message = new HttpRequestMessage(HttpMethod.Post, "/cask-api/v1/AddonDefinition") { Content = new StringContent(body, Encoding.UTF8, Json) };

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Callers.Bearer());

            using var answer = await client.SendAsync(message);

            return (answer.StatusCode, answer.Content.Headers.ContentType?.MediaType, await answer.Content.ReadAsStringAsync());
        }

        [Theory]
        [MemberData(nameof(BodiesWithoutARequiredMember))]
        public async Task TheHost_RefusesADefinitionWithoutAMemberTheDocumentRequires_LeftOutEmptyOrNull(string body, string? sentence)
        {
            var (status, media, answered) = await CreateAsync(body);

            status.Should().Be(HttpStatusCode.BadRequest, answered);

            if (sentence is null)
            {
                media.Should().Be(ProblemJson);
                JsonNode.Parse(answered)!["status"]!.GetValue<int>().Should().Be(400);
            }
            else
            {
                media.Should().Be(Text);
                answered.Should().Be(sentence);
            }
        }

        [Theory]
        [InlineData("""{ "entity": "cask.cask", "path": "handling", "kind": "string" }""")]
        [InlineData("""{ "entity": "cask.cask", "path": "handling", "kind": "string", "values": null, "displayName": null, "description": null }""")]
        [InlineData("""{ "entity": "cask.cask", "path": "handling", "kind": "string", "values": [ { "value": "fragile" }, { "value": "plain", "label": null } ] }""")]
        public async Task TheHost_StoresADefinitionOfTheRequiredMembersAlone(string body)
        {
            var (status, media, answered) = await CreateAsync(body);

            status.Should().Be(HttpStatusCode.Created, answered);
            media.Should().Be(Json);
            JsonSerializer.Deserialize<AddonDefinitionResponse>(answered, Strict)!.Path.Should().Be("handling");
        }

        /// <summary>A token of a user that belongs to no organisation, signed with the host's secret.</summary>
        private static string BearerWithoutOrganisation()
        {
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(BootstrapHost.SigningSecret));
            var token = new JwtSecurityToken(
                claims: [new Claim("Id", Callers.User.ToString())],
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>A store without definitions: every read finds none, and nothing is written in these tests.</summary>
        public class NoDefinitions : DispatchProxy
        {
            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name switch
            {
                nameof(IAddonDefinitionRepository.GetByEntitiesAsync) => Task.FromResult<IEnumerable<AddonDefinitionDocument>>([]),
                nameof(IAddonDefinitionRepository.GetAsync) => Task.FromResult<AddonDefinitionDocument>(null!),
                nameof(IDisposable.Dispose) => null,
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }
    }

    /// <summary>A store without definitions that takes every write.</summary>
    public class StoresEverything : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var answer = targetMethod!.ReturnType;

            if (targetMethod.Name == nameof(IAddonDefinitionRepository.GetByEntitiesAsync))
                return Task.FromResult<IEnumerable<AddonDefinitionDocument>>([]);

            if (answer == typeof(void))
                return null;

            if (answer == typeof(Task))
                return Task.CompletedTask;

            // A task of a value: one write, where the store counts them.
            var value = answer.GetGenericArguments()[0];

            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(value).Invoke(null, [value == typeof(int) ? 1 : null]);
        }
    }

    /// <summary>An entity an organisation adds properties to.</summary>
    [OxQLType("cask.cask", "cask.cask", Extendable = true)]
    public class Cask : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public IDictionary<string, object> Addon { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>A service with an extendable entity and the addon definition routes of the package.</summary>
    public class CaskStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Cask";

        protected override Type[] EntityTypes => [typeof(Cask)];

        protected override Type[] Controllers => [typeof(AddonDefinitionController)];
    }

    /// <summary>An entity of the crate service.</summary>
    [OxQLType("crate.crate", "crate.crate")]
    public class Crate : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public Guid OrganizationId { get; set; }

        public bool IsDeleted { get; set; }

        public string Label { get; set; } = "";
    }

    /// <summary>A service whose document holds the addon definition routes of the package.</summary>
    public class CrateStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Crate";

        protected override Type[] EntityTypes => [typeof(Crate)];

        protected override Type[] Controllers => [typeof(AddonDefinitionController)];
    }
}

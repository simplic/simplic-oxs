using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OxQL.Model.Addon;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Services;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The organisation's addon definitions in descriptor form, and the endpoint that serves them.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaAddonsTests
    {
        private static readonly Guid Organisation = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private sealed class StubSource(params AddonDefinition[] definitions) : IAddonDefinitionSource
        {
            public List<(string Entity, Guid Organisation)> Calls { get; } = [];

            public ValueTask<IReadOnlyList<AddonDefinition>> ForEntityAsync(string entity, Guid organisation, CancellationToken cancellationToken)
            {
                Calls.Add((entity, organisation));

                return ValueTask.FromResult<IReadOnlyList<AddonDefinition>>(definitions.Where(definition => definition.Entity == entity).ToList());
            }
        }

        private static AddonDefinition Definition(string entity, string path, AddonKind kind, bool retired = false, IReadOnlyList<AddonValue>? values = null) => new()
        {
            Id = Guid.NewGuid(),
            Entity = entity,
            Path = path,
            Kind = kind,
            Values = values,
            DisplayName = path == "weight" ? "Weight" : null,
            Description = path == "weight" ? "In kg" : null,
            Retired = retired,
        };

        private static SchemaController Endpoint(IAddonDefinitionSource source, Guid? organisation, string? ifNoneMatch = null)
        {
            var context = new DefaultHttpContext();

            if (ifNoneMatch is not null)
                context.Request.Headers.IfNoneMatch = ifNoneMatch;

            return new SchemaController(SchemaBuild.Degraded, source, new RequestContext { OrganizationId = organisation })
            {
                ControllerContext = new ControllerContext { HttpContext = context },
            };
        }

        [Fact]
        public void Describe_LiveDefinitions_BecomeDescriptorsInPathOrder()
        {
            var descriptors = AddonDescriptors.Describe(
            [
                Definition("probe.widget", "weight", AddonKind.Decimal),
                Definition("probe.widget", "colour", AddonKind.String, values: [new AddonValue("red", "Red"), new AddonValue("blue", null)]),
                Definition("probe.widget", "gone", AddonKind.Int, retired: true),
                Definition("probe.widget", "Ablieferbelege vorhanden", AddonKind.Bool),
                Definition("probe.widget", "vincario", AddonKind.Object),
            ]);

            descriptors.Select(descriptor => descriptor.Name).Should().Equal("Ablieferbelege vorhanden", "colour", "vincario", "weight");
            descriptors.Should().OnlyContain(descriptor => descriptor.Nullable == true);

            var weight = descriptors.Single(descriptor => descriptor.Name == "weight");
            weight.Kind.Should().Be(OxSchemaKinds.Decimal);
            weight.DisplayName.Should().Be("Weight");
            weight.Description.Should().Be("In kg");
            weight.Values.Should().BeNull();

            var colour = descriptors.Single(descriptor => descriptor.Name == "colour");
            colour.Kind.Should().Be(OxSchemaKinds.String);
            colour.Values.Should().HaveCount(2);
            colour.Values![0].Value.Should().Be("red");
            colour.Values[0].Label.Should().Be("Red");
            colour.Values[1].Label.Should().BeNull();

            // An object definition is an untyped container: the engine reads it as unknown, so does the descriptor.
            descriptors.Single(descriptor => descriptor.Name == "vincario").Kind.Should().Be(OxSchemaKinds.Unknown);
        }

        [Fact]
        public async Task BuildAsync_ListsEveryExtendableEntityAndReadsTheSourcePerEntity()
        {
            var source = new StubSource(Definition("probe.widget", "weight", AddonKind.Decimal));

            var result = await AddonDescriptors.BuildAsync(SchemaBuild.Degraded.Model, source, Organisation, CancellationToken.None);

            using var body = JsonDocument.Parse(result.Body);
            var entities = body.RootElement.EnumerateObject().Select(property => property.Name).ToList();

            entities.Should().Equal(SchemaBuild.Degraded.Model.Entities.Values
                .Where(entity => entity.Extendable && Simplic.OxS.Server.OxQL.AddonDefinitionRules.HasBag(entity))
                .Select(entity => entity.Id));
            entities.Should().Contain("probe.widget").And.NotContain("probe.thing");

            // Declared extendable with no addon member. Listing it would publish a
            // key every read refuses with UNKNOWN_PATH, because there is no addon root to bind.
            entities.Should().NotContain("probe.bagless");
            body.RootElement.GetProperty("probe.widget").GetArrayLength().Should().Be(1);
            body.RootElement.GetProperty("probe.widget")[0].GetProperty("name").GetString().Should().Be("weight");
            body.RootElement.GetProperty("probe.widget")[0].GetProperty("kind").GetString().Should().Be("decimal");
            body.RootElement.GetProperty("probe.widget")[0].GetProperty("nullable").GetBoolean().Should().BeTrue();
            source.Calls.Should().OnlyContain(call => call.Organisation == Organisation);
            result.ETag.Should().MatchRegex("^\"[0-9a-f]{64}\"$");
        }

        [Fact]
        public async Task BuildAsync_ETag_FollowsTheList()
        {
            var model = SchemaBuild.Degraded.Model;

            var empty = await AddonDescriptors.BuildAsync(model, new StubSource(), Organisation, CancellationToken.None);
            var same = await AddonDescriptors.BuildAsync(model, new StubSource(), Organisation, CancellationToken.None);
            var one = await AddonDescriptors.BuildAsync(model, new StubSource(Definition("probe.widget", "weight", AddonKind.Decimal)), Organisation, CancellationToken.None);

            empty.ETag.Should().Be(same.ETag);
            one.ETag.Should().NotBe(empty.ETag);
        }

        [Fact]
        public async Task GetAddons_WithoutAnOrganisation_IsForbidden()
        {
            var answer = await Endpoint(new StubSource(), organisation: null).GetAddonsAsync(CancellationToken.None);

            answer.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        }

        [Fact]
        public async Task GetAddons_ServesTheListWithItsTag()
        {
            var endpoint = Endpoint(new StubSource(Definition("probe.widget", "weight", AddonKind.Decimal)), Organisation);

            var file = (await endpoint.GetAddonsAsync(CancellationToken.None)).Should().BeOfType<FileContentResult>().Subject;

            file.ContentType.Should().Be("application/json");
            endpoint.Response.Headers.CacheControl.ToString().Should().Be("private, must-revalidate");
            endpoint.Response.Headers.ETag.ToString().Should().MatchRegex("^\"[0-9a-f]{64}\"$");
            JsonDocument.Parse(file.FileContents).RootElement.GetProperty("probe.widget").GetArrayLength().Should().Be(1);
        }

        [Fact]
        public async Task GetAddons_WithTheCurrentTag_ReturnsNotModified()
        {
            var source = new StubSource(Definition("probe.widget", "weight", AddonKind.Decimal));
            var first = Endpoint(source, Organisation);

            await first.GetAddonsAsync(CancellationToken.None);

            var tag = first.Response.Headers.ETag.ToString();
            var answer = await Endpoint(source, Organisation, tag).GetAddonsAsync(CancellationToken.None);

            answer.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        }

        [Fact]
        public void Schema_StaysAnonymousWhileAddonsNeedTheCaller()
        {
            typeof(SchemaController).GetMethod(nameof(SchemaController.Get))!
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false).Should().NotBeEmpty();
            typeof(SchemaController).GetMethod(nameof(SchemaController.GetAddonsAsync))!
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).Should().NotBeEmpty();
            typeof(SchemaController)
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false).Should().BeEmpty();
        }
    }
}

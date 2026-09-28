using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OxQL.Core.Models;
using OxQL.Model.Addon;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Controllers;
using Simplic.OxS.Server.Controllers.Model;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Services;
using Simplic.OxS.ServiceDefinition;

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

        // ---- retired entity ids: the definitions are the union of the current and every retired id ----

        /// <summary>The fixture host after renaming <c>probe.widget</c> twice.</summary>
        private static readonly Lazy<OxSchemaRegistry> Renamed = new(() => SchemaBuild.Build(SchemaBuild.Options() with
        {
            RetiredEntityIds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["probe.widget"] = ["probe.gizmo", "probe.contraption"],
            },
        }));

        private static AddonDefinitionDocument Stored(string entity, string path, string kind, bool retired = false) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = Organisation,
            Entity = entity,
            Path = path,
            Kind = kind,
            Retired = retired,
        };

        /// <summary>A repository over an in-memory list that answers any entity id and organisation.</summary>
        private static Mock<IAddonDefinitionRepository> Repository(List<AddonDefinitionDocument> rows)
        {
            var repository = new Mock<IAddonDefinitionRepository>(MockBehavior.Strict);

            repository.Setup(r => r.GetByEntitiesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> entities, Guid? _, CancellationToken _) => rows.Where(row => entities.Contains(row.Entity)).ToList());
            repository.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync((Guid id, bool _) => rows.FirstOrDefault(row => row.Id == id)!);
            repository.Setup(r => r.CreateAsync(It.IsAny<AddonDefinitionDocument>()))
                .Callback((AddonDefinitionDocument row) => rows.Add(row))
                .Returns(Task.CompletedTask);
            repository.Setup(r => r.UpdateAsync(It.IsAny<AddonDefinitionDocument>())).Returns(Task.CompletedTask);
            repository.Setup(r => r.CommitAsync()).ReturnsAsync(1);

            return repository;
        }

        private static AddonDefinitionController Api(Mock<IAddonDefinitionRepository> repository, AddonDefinitionCache cache) =>
            new(repository.Object, new RequestContext { OrganizationId = Organisation }, Renamed.Value, cache)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };

        [Fact]
        public async Task Source_ReadsTheCurrentIdAndEveryRetiredId_AndAnswersUnderTheCurrentId()
        {
            var rows = new List<AddonDefinitionDocument>
            {
                Stored("probe.widget", "weight", "decimal"),
                Stored("probe.widget", "shared", "string"),
                Stored("probe.contraption", "shared", "int"),
                Stored("probe.contraption", "gone", "bool", retired: true),
                Stored("probe.gizmo", "legacy", "date"),
            };
            var repository = Repository(rows);
            var source = new AddonDefinitionSource(repository.Object, new AddonDefinitionCache(new OxQLOptions()), Renamed.Value);

            var definitions = await source.ForEntityAsync("probe.widget", Organisation, CancellationToken.None);

            definitions.Select(definition => definition.Path).Should().Equal("weight", "shared", "gone", "legacy");
            definitions.Should().OnlyContain(definition => definition.Entity == "probe.widget", "the engine binds the current id");
            definitions.Single(definition => definition.Path == "shared").Kind.Should().Be(AddonKind.String, "a path under the current id wins over a retired id's");
            definitions.Single(definition => definition.Path == "gone").Retired.Should().BeTrue();
            definitions.Single(definition => definition.Path == "legacy").Kind.Should().Be(AddonKind.Date);

            repository.Verify(r => r.GetByEntitiesAsync(
                It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "probe.widget", "probe.contraption", "probe.gizmo" })),
                Organisation,
                It.IsAny<CancellationToken>()), Times.Once, "the current id and every retired id are one read");
        }

        [Fact]
        public async Task Source_AskedForARetiredId_ServesTheCurrentEntityFromOneCacheEntry()
        {
            var repository = Repository([Stored("probe.gizmo", "legacy", "date")]);
            var cache = new AddonDefinitionCache(new OxQLOptions());
            var source = new AddonDefinitionSource(repository.Object, cache, Renamed.Value);

            var byRetired = await source.ForEntityAsync("probe.gizmo", Organisation, CancellationToken.None);
            var byCurrent = await source.ForEntityAsync("probe.widget", Organisation, CancellationToken.None);

            byCurrent.Should().BeSameAs(byRetired);
            byRetired.Should().ContainSingle().Which.Entity.Should().Be("probe.widget");
            cache.TryGet(Organisation, "probe.gizmo", out _).Should().BeFalse();
            repository.Verify(r => r.GetByEntitiesAsync(It.IsAny<IReadOnlyCollection<string>>(), Organisation, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BuildAsync_ThroughTheSource_PublishesRetiredIdDefinitionsUnderTheCurrentEntity()
        {
            var repository = Repository([Stored("probe.widget", "weight", "decimal"), Stored("probe.gizmo", "legacy", "date")]);
            var source = new AddonDefinitionSource(repository.Object, new AddonDefinitionCache(new OxQLOptions()), Renamed.Value);

            var result = await AddonDescriptors.BuildAsync(Renamed.Value.Model, source, Organisation, CancellationToken.None);

            using var body = JsonDocument.Parse(result.Body);
            body.RootElement.TryGetProperty("probe.gizmo", out _).Should().BeFalse();
            body.RootElement.GetProperty("probe.widget").EnumerateArray()
                .Select(descriptor => descriptor.GetProperty("name").GetString()).Should().Equal("legacy", "weight");
        }

        [Theory]
        [InlineData("probe.widget")]
        [InlineData("probe.gizmo")]
        public async Task Api_GetByEntity_ListsTheUnionUnderTheCurrentId(string asked)
        {
            var repository = Repository([Stored("probe.widget", "weight", "decimal"), Stored("probe.gizmo", "legacy", "date")]);

            var answer = await Api(repository, new AddonDefinitionCache(new OxQLOptions())).GetByEntityAsync(asked, CancellationToken.None);

            var listed = answer.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeAssignableTo<IEnumerable<AddonDefinitionResponse>>().Subject.ToList();
            listed.Select(definition => definition.Path).Should().Equal("legacy", "weight");
            listed.Should().OnlyContain(definition => definition.Entity == "probe.widget");
        }

        [Fact]
        public async Task Api_Create_RevivesARetiredRowOfARetiredIdAndMovesItToTheCurrentId()
        {
            var old = Stored("probe.gizmo", "weight", "decimal", retired: true);
            var rows = new List<AddonDefinitionDocument> { old };
            var cache = new AddonDefinitionCache(new OxQLOptions());
            cache.Set(Organisation, "probe.widget", []);

            var answer = await Api(Repository(rows), cache).CreateAsync(
                new CreateAddonDefinitionRequest { Entity = "probe.gizmo", Path = "weight", Kind = "decimal" }, CancellationToken.None);

            answer.Should().BeOfType<CreatedAtActionResult>().Which.Value.Should().BeOfType<AddonDefinitionResponse>().Which.Entity.Should().Be("probe.widget");
            rows.Should().ContainSingle().Which.Should().BeSameAs(old);
            old.Retired.Should().BeFalse();
            old.Entity.Should().Be("probe.widget");
            cache.TryGet(Organisation, "probe.widget", out _).Should().BeFalse("the write invalidates the entry the engine reads");
        }

        [Fact]
        public async Task Api_Create_RefusesAPathLiveUnderARetiredId()
        {
            var rows = new List<AddonDefinitionDocument> { Stored("probe.contraption", "weight", "decimal") };

            var answer = await Api(Repository(rows), new AddonDefinitionCache(new OxQLOptions())).CreateAsync(
                new CreateAddonDefinitionRequest { Entity = "probe.widget", Path = "weight", Kind = "decimal" }, CancellationToken.None);

            answer.Should().BeOfType<ConflictObjectResult>();
            rows.Should().HaveCount(1);
        }

        [Fact]
        public async Task Api_Delete_OfARetiredIdRow_InvalidatesTheCurrentEntity()
        {
            var old = Stored("probe.gizmo", "legacy", "date");
            var cache = new AddonDefinitionCache(new OxQLOptions());
            cache.Set(Organisation, "probe.widget", []);

            var answer = await Api(Repository([old]), cache).DeleteAsync(old.Id, CancellationToken.None);

            answer.Should().BeOfType<NoContentResult>();
            old.Retired.Should().BeTrue();
            old.Entity.Should().Be("probe.widget");
            cache.TryGet(Organisation, "probe.widget", out _).Should().BeFalse();
        }

        [Fact]
        public async Task Source_PrefersALiveRowOfARetiredIdOverARetiredRowOfTheCurrentId()
        {
            var live = Stored("probe.gizmo", "colour", "string");
            var repository = Repository([Stored("probe.widget", "colour", "string", retired: true), live]);
            var source = new AddonDefinitionSource(repository.Object, new AddonDefinitionCache(new OxQLOptions()), Renamed.Value);

            var definitions = await source.ForEntityAsync("probe.widget", Organisation, CancellationToken.None);

            definitions.Should().ContainSingle().Which.Should().Match<AddonDefinition>(definition => definition.Id == live.Id && !definition.Retired);
        }

        [Fact]
        public void Union_BetweenRowsAlike_ReadsTheIdListedFirst_InTheOrderOfTheIds()
        {
            var retiredCurrent = Stored("probe.widget", "a", "int", retired: true);
            var retiredOld = Stored("probe.gizmo", "a", "int", retired: true);
            var other = Stored("probe.widget", "b", "int");

            var union = AddonDefinitionSource.Union(["probe.widget", "probe.gizmo"], [retiredOld, other, retiredCurrent]);

            union.Should().Equal(other, retiredCurrent);
        }

        [Fact]
        public async Task Source_ForwardsTheRequestsCancellationToken()
        {
            using var cancellation = new CancellationTokenSource();
            var repository = Repository([]);
            var source = new AddonDefinitionSource(repository.Object, new AddonDefinitionCache(new OxQLOptions()), Renamed.Value);

            await source.ForEntityAsync("probe.widget", Organisation, cancellation.Token);

            repository.Verify(r => r.GetByEntitiesAsync(It.IsAny<IReadOnlyCollection<string>>(), Organisation, cancellation.Token), Times.Once);
        }

        [Fact]
        public async Task Api_GetByEntity_ForwardsTheRequestsCancellationToken()
        {
            using var cancellation = new CancellationTokenSource();
            var repository = Repository([]);

            await Api(repository, new AddonDefinitionCache(new OxQLOptions())).GetByEntityAsync("probe.widget", cancellation.Token);

            repository.Verify(r => r.GetByEntitiesAsync(It.IsAny<IReadOnlyCollection<string>>(), null, cancellation.Token), Times.Once);
        }

        [Fact]
        public async Task Api_Update_OfARetiredIdRow_MovesItToTheCurrentIdAndInvalidatesTheCurrentEntity()
        {
            var old = Stored("probe.gizmo", "legacy", "string");
            var cache = new AddonDefinitionCache(new OxQLOptions());
            cache.Set(Organisation, "probe.widget", []);

            var answer = await Api(Repository([old]), cache).UpdateAsync(old.Id, new UpdateAddonDefinitionRequest { DisplayName = "Legacy" }, CancellationToken.None);

            answer.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<AddonDefinitionResponse>().Which.Entity.Should().Be("probe.widget");
            old.Entity.Should().Be("probe.widget");
            old.DisplayName.Should().Be("Legacy");
            cache.TryGet(Organisation, "probe.widget", out _).Should().BeFalse("the write invalidates the entry the engine reads");
        }

        [Fact]
        public async Task Api_Update_OfARetiredIdRowWhosePathTheCurrentIdHolds_KeepsItUnderItsId()
        {
            var hidden = Stored("probe.widget", "colour", "string", retired: true);
            var live = Stored("probe.gizmo", "colour", "string");
            var rows = new List<AddonDefinitionDocument> { hidden, live };
            var cache = new AddonDefinitionCache(new OxQLOptions());
            cache.Set(Organisation, "probe.widget", []);

            var answer = await Api(Repository(rows), cache).UpdateAsync(live.Id, new UpdateAddonDefinitionRequest { DisplayName = "Colour" }, CancellationToken.None);

            answer.Should().BeOfType<OkObjectResult>();
            live.Entity.Should().Be("probe.gizmo", "the current id already holds the path, and one id never holds two rows of one path");
            rows.Where(row => row.Entity == "probe.widget" && row.Path == "colour").Should().ContainSingle();
            cache.TryGet(Organisation, "probe.widget", out _).Should().BeFalse();

            var listed = await Api(Repository(rows), new AddonDefinitionCache(new OxQLOptions())).GetByEntityAsync("probe.widget", CancellationToken.None);
            listed.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeAssignableTo<IEnumerable<AddonDefinitionResponse>>()
                .Which.Should().ContainSingle().Which.Should().Match<AddonDefinitionResponse>(row => row.Id == live.Id && row.DisplayName == "Colour" && !row.Retired);
        }

        [Fact]
        public async Task Api_Delete_OfARetiredIdRowWhosePathTheCurrentIdHolds_RetiresItUnderItsId()
        {
            var hidden = Stored("probe.widget", "colour", "string", retired: true);
            var live = Stored("probe.gizmo", "colour", "string");
            var rows = new List<AddonDefinitionDocument> { hidden, live };

            var answer = await Api(Repository(rows), new AddonDefinitionCache(new OxQLOptions())).DeleteAsync(live.Id, CancellationToken.None);

            answer.Should().BeOfType<NoContentResult>();
            live.Retired.Should().BeTrue();
            live.Entity.Should().Be("probe.gizmo");
            rows.Where(row => row.Entity == "probe.widget" && row.Path == "colour").Should().ContainSingle();
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

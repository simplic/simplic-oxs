using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OxQL.Core.Models;
using Simplic.OxS.Server.Controllers;
using Simplic.OxS.Server.Controllers.Model;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.Services;
using Simplic.OxS.Server.Test.OxSchema;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>The addon definition API: refusals on creation, immutability of path and kind, retirement, and cache invalidation.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class AddonDefinitionControllerTests
    {
        private static readonly Guid Organisation = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private sealed class Harness
        {
            public Mock<IAddonDefinitionRepository> Repository { get; } = new(MockBehavior.Strict);
            public List<AddonDefinitionDocument> Stored { get; } = [];
            public AddonDefinitionCache Cache { get; } = new(new OxQLOptions());
            public AddonDefinitionController Controller { get; }

            public Harness(Guid? organisation = null, params AddonDefinitionDocument[] existing)
            {
                Stored.AddRange(existing);

                Repository.Setup(repository => repository.GetByEntitiesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlyCollection<string> entities, Guid? _, CancellationToken _) => Stored.Where(definition => entities.Contains(definition.Entity)).ToList());
                Repository.Setup(repository => repository.GetAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                    .ReturnsAsync((Guid id, bool _) => Stored.FirstOrDefault(definition => definition.Id == id)!);
                Repository.Setup(repository => repository.CreateAsync(It.IsAny<AddonDefinitionDocument>()))
                    .Callback((AddonDefinitionDocument definition) => Stored.Add(definition))
                    .Returns(Task.CompletedTask);
                Repository.Setup(repository => repository.UpdateAsync(It.IsAny<AddonDefinitionDocument>())).Returns(Task.CompletedTask);
                Repository.Setup(repository => repository.CommitAsync()).ReturnsAsync(1);

                Controller = new AddonDefinitionController(
                    Repository.Object,
                    new RequestContext { OrganizationId = organisation ?? Organisation },
                    SchemaBuild.Degraded,
                    Cache)
                {
                    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                };
            }
        }

        private static AddonDefinitionDocument Existing(string path, string kind, bool retired = false, Guid? organisation = null) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = organisation ?? Organisation,
            Entity = "probe.widget",
            Path = path,
            Kind = kind,
            Retired = retired,
        };

        private static CreateAddonDefinitionRequest Create(string path = "weight", string kind = "decimal", string entity = "probe.widget") =>
            new() { Entity = entity, Path = path, Kind = kind, DisplayName = "Weight" };

        [Fact]
        public async Task Create_StoresTheDefinitionAndAnswersCreated()
        {
            var harness = new Harness();
            harness.Cache.Set(Organisation, "probe.widget", []);

            var answer = await harness.Controller.CreateAsync(Create(), CancellationToken.None);

            var created = answer.Should().BeOfType<CreatedAtActionResult>().Subject;
            var response = created.Value.Should().BeOfType<AddonDefinitionResponse>().Subject;

            response.Entity.Should().Be("probe.widget");
            response.Path.Should().Be("weight");
            response.Kind.Should().Be("decimal");
            response.Retired.Should().BeFalse();
            harness.Stored.Should().ContainSingle().Which.OrganizationId.Should().Be(Organisation);
            harness.Repository.Verify(repository => repository.CommitAsync(), Times.Once);
            harness.Cache.TryGet(Organisation, "probe.widget", out _).Should().BeFalse("a write invalidates the engine's view");
        }

        [Theory]
        [InlineData("probe.nothing", "weight", "decimal")]
        [InlineData("probe.thing", "weight", "decimal")]
        [InlineData("probe.widget", "a..b", "decimal")]
        [InlineData("probe.widget", "$weight", "decimal")]
        [InlineData("probe.widget", "weight", "Decimal")]
        [InlineData("probe.widget", "weight", "array")]
        public async Task Create_RefusesTheEntityPathAndKindRules(string entity, string path, string kind)
        {
            var harness = new Harness();

            var answer = await harness.Controller.CreateAsync(Create(path, kind, entity), CancellationToken.None);

            answer.Should().BeOfType<BadRequestObjectResult>();
            harness.Stored.Should().BeEmpty();
        }

        [Fact]
        public async Task Create_RefusesAValueListOnADecimal()
        {
            var harness = new Harness();
            var request = Create();
            request.Values = [new AddonDefinitionValueRequest { Value = "1" }];

            (await harness.Controller.CreateAsync(request, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Create_RefusesADuplicatePathWithConflict()
        {
            var harness = new Harness(existing: Existing("weight", "decimal"));

            (await harness.Controller.CreateAsync(Create(), CancellationToken.None)).Should().BeOfType<ConflictObjectResult>();
            harness.Stored.Should().HaveCount(1);
        }

        [Fact]
        public async Task Create_RefusesShadowing()
        {
            var harness = new Harness(existing: Existing("weight", "decimal"));

            (await harness.Controller.CreateAsync(Create("weight.unit", "string"), CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Create_RevivesARetiredDefinitionOfTheSameKind()
        {
            var retired = Existing("weight", "decimal", retired: true);
            var harness = new Harness(existing: retired);

            var answer = await harness.Controller.CreateAsync(Create(), CancellationToken.None);

            answer.Should().BeOfType<CreatedAtActionResult>();
            harness.Stored.Should().ContainSingle().Which.Should().BeSameAs(retired);
            retired.Retired.Should().BeFalse();
            retired.DisplayName.Should().Be("Weight");
            harness.Repository.Verify(repository => repository.CreateAsync(It.IsAny<AddonDefinitionDocument>()), Times.Never);
            harness.Repository.Verify(repository => repository.UpdateAsync(retired), Times.Once);
        }

        [Fact]
        public async Task Create_RefusesToReviveARetiredDefinitionUnderAnotherKind()
        {
            var harness = new Harness(existing: Existing("weight", "decimal", retired: true));

            (await harness.Controller.CreateAsync(Create("weight", "string"), CancellationToken.None)).Should().BeOfType<ConflictObjectResult>();
        }

        [Fact]
        public async Task Create_WithoutAnOrganisation_IsForbidden()
        {
            var harness = new Harness();
            var controller = new AddonDefinitionController(harness.Repository.Object, new RequestContext(), SchemaBuild.Degraded, harness.Cache);

            (await controller.CreateAsync(Create(), CancellationToken.None)).Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task GetByEntity_ListsRetiredOnesFlaggedAndRefusesAnUnknownEntity()
        {
            var harness = new Harness(null, Existing("weight", "decimal"), Existing("colour", "string", retired: true));

            var ok = (await harness.Controller.GetByEntityAsync("probe.widget", CancellationToken.None)).Should().BeOfType<OkObjectResult>().Subject;
            var list = ok.Value.Should().BeAssignableTo<IEnumerable<AddonDefinitionResponse>>().Subject.ToList();

            list.Select(response => response.Path).Should().Equal("colour", "weight");
            list.Single(response => response.Path == "colour").Retired.Should().BeTrue();

            (await harness.Controller.GetByEntityAsync("probe.nothing", CancellationToken.None)).Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Update_ChangesLabelsAndValuesOnly()
        {
            var existing = Existing("colour", "string");
            var harness = new Harness(existing: existing);
            harness.Cache.Set(Organisation, "probe.widget", []);

            var answer = await harness.Controller.UpdateAsync(existing.Id, new UpdateAddonDefinitionRequest
            {
                DisplayName = "Colour",
                Description = "Paint",
                Values = [new AddonDefinitionValueRequest { Value = "red", Label = "Red" }],
            }, CancellationToken.None);

            var response = answer.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<AddonDefinitionResponse>().Subject;

            response.Path.Should().Be("colour");
            response.Kind.Should().Be("string");
            response.DisplayName.Should().Be("Colour");
            response.Values.Should().ContainSingle().Which.Label.Should().Be("Red");
            harness.Cache.TryGet(Organisation, "probe.widget", out _).Should().BeFalse();
        }

        [Fact]
        public async Task Update_RefusesAValueListTheKindDoesNotAllow()
        {
            var existing = Existing("weight", "decimal");
            var harness = new Harness(existing: existing);

            var answer = await harness.Controller.UpdateAsync(existing.Id, new UpdateAddonDefinitionRequest
            {
                Values = [new AddonDefinitionValueRequest { Value = "1" }],
            }, CancellationToken.None);

            answer.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task Update_AndDelete_AnswerNotFoundForAnotherOrganisationsDefinition()
        {
            var foreign = Existing("weight", "decimal", organisation: Other);
            var harness = new Harness(existing: foreign);

            (await harness.Controller.UpdateAsync(foreign.Id, new UpdateAddonDefinitionRequest(), CancellationToken.None)).Should().BeOfType<NotFoundResult>();
            (await harness.Controller.DeleteAsync(foreign.Id, CancellationToken.None)).Should().BeOfType<NotFoundResult>();
            (await harness.Controller.GetByIdAsync(foreign.Id, CancellationToken.None)).Should().BeOfType<NotFoundResult>();
            foreign.Retired.Should().BeFalse();
        }

        [Fact]
        public async Task Delete_RetiresTheDefinitionAndKeepsTheRow()
        {
            var existing = Existing("weight", "decimal");
            var harness = new Harness(existing: existing);
            harness.Cache.Set(Organisation, "probe.widget", []);

            (await harness.Controller.DeleteAsync(existing.Id, CancellationToken.None)).Should().BeOfType<NoContentResult>();

            existing.Retired.Should().BeTrue();
            harness.Stored.Should().ContainSingle();
            harness.Repository.Verify(repository => repository.UpdateAsync(existing), Times.Once);
            harness.Cache.TryGet(Organisation, "probe.widget", out _).Should().BeFalse();

            // A second delete is idempotent and writes nothing.
            (await harness.Controller.DeleteAsync(existing.Id, CancellationToken.None)).Should().BeOfType<NoContentResult>();
            harness.Repository.Verify(repository => repository.UpdateAsync(existing), Times.Once);
        }
    }
}

using OxQL.Core.Models;
using OxQL.Model.Addon;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>The engine's per-request view of the definitions: read through the cache, invalidated on write.</summary>
    public sealed class AddonDefinitionSourceTests
    {
        private static readonly Guid Organisation = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static AddonDefinitionDocument Document(string path, string kind, bool retired = false) => new()
        {
            Id = Guid.NewGuid(),
            OrganizationId = Organisation,
            Entity = "probe.widget",
            Path = path,
            Kind = kind,
            Retired = retired,
            Values = kind == "string" ? [new AddonDefinitionValue { Value = "red", Label = "Red" }] : null,
            DisplayName = "Label",
        };

        [Fact]
        public async Task ForEntity_ReadsTheRepositoryOnceAndThenTheCache()
        {
            var repository = new Mock<IAddonDefinitionRepository>(MockBehavior.Strict);
            repository.Setup(r => r.GetByEntityAsync("probe.widget", Organisation))
                .ReturnsAsync([Document("weight", "decimal"), Document("colour", "string"), Document("gone", "int", retired: true)]);

            var cache = new AddonDefinitionCache(new OxQLOptions());
            var source = new AddonDefinitionSource(repository.Object, cache);

            var first = await source.ForEntityAsync("probe.widget", Organisation, CancellationToken.None);
            var second = await source.ForEntityAsync("probe.widget", Organisation, CancellationToken.None);

            repository.Verify(r => r.GetByEntityAsync("probe.widget", Organisation), Times.Once);
            second.Should().BeSameAs(first);
            first.Should().HaveCount(3, "retired definitions are included, as the engine's contract asks");
            first.Single(definition => definition.Path == "gone").Retired.Should().BeTrue();
            first.Single(definition => definition.Path == "weight").Kind.Should().Be(AddonKind.Decimal);
            first.Single(definition => definition.Path == "colour").Values.Should().ContainSingle().Which.Should().Be(new AddonValue("red", "Red"));
        }

        [Fact]
        public async Task ForEntity_AfterInvalidation_ReadsTheRepositoryAgain()
        {
            var repository = new Mock<IAddonDefinitionRepository>(MockBehavior.Strict);
            repository.Setup(r => r.GetByEntityAsync("probe.widget", Organisation)).ReturnsAsync([]);

            var cache = new AddonDefinitionCache(new OxQLOptions());
            var source = new AddonDefinitionSource(repository.Object, cache);

            await source.ForEntityAsync("probe.widget", Organisation, CancellationToken.None);
            cache.Invalidate(Organisation, "probe.widget");
            await source.ForEntityAsync("probe.widget", Organisation, CancellationToken.None);

            repository.Verify(r => r.GetByEntityAsync("probe.widget", Organisation), Times.Exactly(2));
        }

        [Fact]
        public void ToDefinition_ReadsAnUnknownStoredKindAsAnUntypedContainer()
        {
            AddonDefinitionSource.ToDefinition(Document("odd", "something")).Kind.Should().Be(AddonKind.Object);
        }

        [Fact]
        public void Cache_KeysPerOrganisationAndEntity()
        {
            var cache = new AddonDefinitionCache(new OxQLOptions());
            var other = Guid.NewGuid();

            cache.Set(Organisation, "probe.widget", []);

            cache.TryGet(Organisation, "probe.widget", out _).Should().BeTrue();
            cache.TryGet(other, "probe.widget", out _).Should().BeFalse();
            cache.TryGet(Organisation, "probe.thing", out _).Should().BeFalse();
        }
    }
}

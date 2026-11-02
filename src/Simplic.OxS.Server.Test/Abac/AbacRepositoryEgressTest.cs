using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Moq;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.Data;
using Simplic.OxS.Data.MongoDB;
using Simplic.OxS.Data.MongoDB.Abac;
using Xunit;

namespace Simplic.OxS.Server.Test.Abac;

/// <summary>Tests the opt-in ABAC egress hook of <see cref="MongoOrganizationRepositoryBase{TDocument, TFilter}"/>.</summary>
public class AbacRepositoryEgressTest
{
    public class ContactDocument : OrganizationDocumentBase
    {
        public string Department { get; set; } = string.Empty;
    }

    private sealed class ContactRepository : MongoOrganizationRepositoryBase<ContactDocument, OrganizationFilterBase>
    {
        private readonly string? _action;

        public ContactRepository(string? action, IAbacReadFilterProvider? provider)
            : base(Mock.Of<IMongoContext>(), Mock.Of<IRequestContext>(), provider)
        {
            _action = action;
        }

        protected override string? AbacReadAction => _action;

        protected override string GetCollectionName() => "contacts";

        public Task<FilterDefinition<ContactDocument>> Apply(FilterDefinition<ContactDocument> filter)
            => ApplyAbacReadFilterAsync(filter);
    }

    private static BsonDocument Render(FilterDefinition<ContactDocument> filter)
        => filter.Render(new RenderArgs<ContactDocument>(BsonSerializer.LookupSerializer<ContactDocument>(), BsonSerializer.SerializerRegistry));

    private static Mock<IAbacReadFilterProvider> Provider(AbacDecision decision, string? filter = null)
    {
        var provider = new Mock<IAbacReadFilterProvider>();
        provider.Setup(p => p.GetReadFilterAsync("oxs:contact:read", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AbacDecisionResult { Decision = decision, MongoDbFilterJson = filter });
        return provider;
    }

    private static readonly FilterDefinition<ContactDocument> BaseFilter = Builders<ContactDocument>.Filter.Eq(x => x.IsDeleted, false);

    [Fact]
    public async Task NoAction_FilterUnchanged_ProviderNotCalled()
    {
        var provider = Provider(AbacDecision.Deny);

        var result = await new ContactRepository(null, provider.Object).Apply(BaseFilter);

        result.Should().BeSameAs(BaseFilter);
        provider.Verify(p => p.GetReadFilterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoProvider_FilterUnchanged()
        => (await new ContactRepository("oxs:contact:read", null).Apply(BaseFilter)).Should().BeSameAs(BaseFilter);

    [Fact]
    public async Task Deny_ThrowsAccessDenied()
    {
        var act = () => new ContactRepository("oxs:contact:read", Provider(AbacDecision.Deny).Object).Apply(BaseFilter);

        (await act.Should().ThrowAsync<AbacAccessDeniedException>()).Which.Action.Should().Be("oxs:contact:read");
    }

    [Fact]
    public async Task Allow_WithFilter_IsAndedIntoQuery()
    {
        var provider = Provider(AbacDecision.Allow, "{\"$nor\":[{\"Department\":\"hr\"}]}");

        var rendered = Render(await new ContactRepository("oxs:contact:read", provider.Object).Apply(BaseFilter)).ToJson();

        rendered.Should().Contain("IsDeleted").And.Contain("$nor").And.Contain("hr");
    }

    [Fact]
    public async Task Allow_WithoutFilter_IsUnrestricted()
    {
        var rendered = Render(await new ContactRepository("oxs:contact:read", Provider(AbacDecision.Allow).Object).Apply(BaseFilter));

        rendered.Should().BeEquivalentTo(Render(BaseFilter));
    }

    [Fact]
    public async Task SuppressedScope_SkipsFilter()
    {
        var provider = Provider(AbacDecision.Deny);

        using (AbacDataScope.SuppressReadFilter())
        {
            (await new ContactRepository("oxs:contact:read", provider.Object).Apply(BaseFilter)).Should().BeSameAs(BaseFilter);
        }

        await FluentActions.Awaiting(() => new ContactRepository("oxs:contact:read", provider.Object).Apply(BaseFilter))
            .Should().ThrowAsync<AbacAccessDeniedException>();
    }
}

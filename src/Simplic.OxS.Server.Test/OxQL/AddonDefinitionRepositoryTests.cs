using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Simplic.OxS.ServiceDefinition;
using Simplic.OxS.ServiceDefinition.Repository;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>The addon definition store: one collection per service, and the unique index over organisation, entity id and path.</summary>
    public sealed class AddonDefinitionRepositoryTests
    {
        [Theory]
        [InlineData("vehicle", "model_definition.addon_definition.vehicle")]
        [InlineData("Logistics", "model_definition.addon_definition.logistics")]
        [InlineData(" sequence-number ", "model_definition.addon_definition.sequence-number")]
        public void CollectionNameOf_CarriesTheLowerCasedServiceName(string service, string expected)
        {
            AddonDefinitionRepository.CollectionNameOf(service).Should().Be(expected);
        }

        [Fact]
        public void CollectionNameOf_TwoServicesNeverShareACollection()
        {
            AddonDefinitionRepository.CollectionNameOf("auth").Should().NotBe(AddonDefinitionRepository.CollectionNameOf("contact"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void CollectionNameOf_RefusesAHostThatNamesNoService(string? service)
        {
            FluentActions.Invoking(() => AddonDefinitionRepository.CollectionNameOf(service)).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void UniqueIndex_IsOverOrganisationEntityAndPathAmongTheRowsNotDeleted()
        {
            var index = AddonDefinitionRepository.UniqueIndex();
            var args = new RenderArgs<AddonDefinitionDocument>(BsonSerializer.SerializerRegistry.GetSerializer<AddonDefinitionDocument>(), BsonSerializer.SerializerRegistry);
            var options = index.Options.Should().BeOfType<CreateIndexOptions<AddonDefinitionDocument>>().Subject;

            index.Keys.Render(args).ToJson().Should().Be("{ \"OrganizationId\" : 1, \"Entity\" : 1, \"Path\" : 1 }");
            options.Unique.Should().BeTrue();
            options.Name.Should().Be("organization_entity_path_unique");
            options.PartialFilterExpression.Render(args).ToJson().Should().Be("{ \"IsDeleted\" : false }");
        }

        [Fact]
        public void IsDuplicateKey_ReadsOnlyTheUniqueIndexRefusal()
        {
            AddonDefinitionRepository.IsDuplicateKey(new MongoException("something else")).Should().BeFalse();
        }
    }
}

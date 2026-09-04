using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>Foreign keys, the field they resolve to, and embedded snapshots.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaRelationshipTests
    {
        [Fact]
        public void Build_DeclaredReference_NamesTheTargetAndIsNotInferred()
        {
            var reference = SchemaBuild.Degraded.Document.Property("probe.link", "thingId").References;

            reference.Should().NotBeNull();
            reference!.Entity.Should().Be("probe.thing");
            reference.Inferred.Should().BeFalse();
        }

        [Fact]
        public void Build_ConventionalIdName_ResolvesOnTheNameAloneAndIsInferred()
        {
            var reference = SchemaBuild.Degraded.Document.Property("probe.link", "widgetId").References;

            reference.Should().NotBeNull();
            reference!.Entity.Should().Be("probe.widget");
            reference.Inferred.Should().BeTrue();
        }

        [Fact]
        public void Build_GuidSuffix_ResolvesLikeTheIdSuffix()
        {
            var reference = SchemaBuild.Degraded.Document.Property("probe.link", "thingGuid").References;

            reference.Should().NotBeNull();
            reference!.Entity.Should().Be("probe.thing");
            reference.Inferred.Should().BeTrue();
        }

        [Theory]
        [InlineData("otherThingId")]
        [InlineData("gadgetId")]
        [InlineData("registratorId")]
        [InlineData("subsetId")]
        [InlineData("id")]
        public void Build_UnresolvableStem_PublishesNoReference(string name)
        {
            SchemaBuild.Degraded.Document.Property("probe.link", name).References.Should().BeNull();
        }

        [Fact]
        public void Build_DeclarationNamingAnAbsentProperty_AddsNoMember()
        {
            var names = SchemaBuild.Degraded.Document.PropertyNames("probe.link");

            names.Should().NotContain("missingId");
        }

        [Fact]
        public void Build_DeclarationWithANonEntityNavigation_LeavesBothMembersBare()
        {
            var document = SchemaBuild.Degraded.Document;

            var navigation = document.Property("probe.link", "subset");

            navigation.Target().Should().Be("t_thingSubset");
            navigation.SnapshotOf.Should().BeNull();
            navigation.References.Should().BeNull();
        }

        [Fact]
        public void Build_ReferenceField_ComesFromTheTargetsOwnKey()
        {
            SchemaBuild.Degraded.Document.Property("probe.link", "widgetId").References!.Field.Should().Be("id");
        }

        [Theory]
        [InlineData("thingId")]
        [InlineData("thingGuid")]
        public void Build_ReferenceToAnEntityWithNoKey_PublishesNoField(string name)
        {
            SchemaBuild.Degraded.Document.Property("probe.link", name).References!.Field.Should().BeNull();
        }

        [Fact]
        public void Build_Reference_IsNeverJoinable()
        {
            var document = SchemaBuild.Degraded.Document;

            var references = document.Types
                .SelectMany(entry => entry.Value.Properties ?? [])
                .Select(property => property.References)
                .Where(reference => reference is not null);

            references.Should().NotBeEmpty();
            references.Should().OnlyContain(reference => !reference!.Joinable);
        }

        [Fact]
        public void Build_EmbeddedEntity_CarriesTheSnapshotBesideThePointer()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.link", "single");

            property.Target().Should().Be("probe.thing");
            property.SnapshotOf.Should().Be("probe.thing");
        }

        [Fact]
        public void Build_EmbeddedEntityInAnArray_CarriesTheSnapshotOnTheElement()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.link", "many");

            property.SnapshotOf.Should().BeNull();
            property.Of!.Target().Should().Be("probe.thing");
            property.Of.SnapshotOf.Should().Be("probe.thing");
        }

        [Fact]
        public void Build_EmbeddedEntityInADictionary_CarriesTheSnapshotOnTheValue()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.link", "keyed");

            property.SnapshotOf.Should().BeNull();
            property.Value!.Target().Should().Be("probe.thing");
            property.Value.SnapshotOf.Should().Be("probe.thing");
        }

        [Fact]
        public void Build_OwnedEmbeddedShape_CarriesNoSnapshotAndNoReference()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.link", "slots");

            property.Of!.Target().Should().Be("t_slot");
            property.Of.SnapshotOf.Should().BeNull();
            property.References.Should().BeNull();
        }

        [Fact]
        public void Build_NavigationDeclaredWithAnAbsentIdProperty_IsStillDescribedAsASnapshot()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.link", "absent");

            property.Kind.Should().Be(OxSchemaKinds.Object);
            property.Target().Should().Be("probe.thing");
            property.SnapshotOf.Should().Be("probe.thing");
        }
    }
}

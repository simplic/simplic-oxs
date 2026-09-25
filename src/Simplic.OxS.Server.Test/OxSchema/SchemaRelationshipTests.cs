using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>
    /// Foreign keys and embedded snapshots. A reference is a declaration on the model, never a
    /// guess from a member's name: the query engine follows exactly what the document publishes.
    /// </summary>
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

        [Theory]
        [InlineData("widgetId")]
        [InlineData("thingGuid")]
        public void Build_ConventionalIdName_IsNoLongerAReference(string name)
        {
            // The naming convention is retired: an id member references an entity only when declared.
            SchemaBuild.Degraded.Document.Property("probe.link", name).References.Should().BeNull();
        }

        [Theory]
        [InlineData("otherThingId")]
        [InlineData("gadgetId")]
        [InlineData("registratorId")]
        [InlineData("subsetId")]
        [InlineData("id")]
        public void Build_UndeclaredIdMember_PublishesNoReference(string name)
        {
            SchemaBuild.Degraded.Document.Property("probe.link", name).References.Should().BeNull();
        }

        [Fact]
        public void Build_NoInferredReference_ExistsAnywhere()
        {
            var references = SchemaBuild.Degraded.Document.Types
                .SelectMany(entry => entry.Value.Properties ?? [])
                .Select(property => property.References)
                .Where(reference => reference is not null)
                .ToList();

            references.Should().NotBeEmpty();
            references.Should().OnlyContain(reference => !reference!.Inferred);
        }

        [Fact]
        public void Build_DeclarationNamingAnAbsentProperty_AddsNoMember()
        {
            var names = SchemaBuild.Degraded.Document.PropertyNames("probe.link");

            names.Should().NotContain("missingId");
        }

        [Fact]
        public void Build_DeclarationNamingAnAbsentProperty_IsReported()
        {
            SchemaBuild.Degraded.Findings.Should().Contain(finding =>
                finding.Code == OxSchemaCodes.ReferenceDeclarationUnresolved && finding.Target == "probe.link#absent");
        }

        [Fact]
        public void Build_DeclarationWithANonEntityNavigation_LeavesBothMembersBare()
        {
            var document = SchemaBuild.Degraded.Document;

            var navigation = document.Property("probe.link", "subset");

            navigation.Target().Should().Be("t_thingSubset");
            navigation.SnapshotOf.Should().BeNull();
            navigation.References.Should().BeNull();
            document.Property("probe.link", "subsetId").References.Should().BeNull();
        }

        [Fact]
        public void Build_ReferenceField_IsTheTargetsStoredKey()
        {
            // The engine joins on the target's `_id`, wire `id`, whether or not the target
            // declares an identity interface; the document publishes the field the engine uses.
            SchemaBuild.Degraded.Document.Property("probe.link", "thingId").References!.Field.Should().Be("id");
        }

        [Fact]
        public void Build_Reference_IsDeclaredAndJoinable()
        {
            var document = SchemaBuild.Degraded.Document;

            var references = document.Types
                .SelectMany(entry => entry.Value.Properties ?? [])
                .Select(property => property.References)
                .Where(reference => reference is not null);

            references.Should().NotBeEmpty();

            // A declared reference is what makes a lookup or a resolve legal under contract 2,
            // and name inference is gone, so every reference in the document is both.
            references.Should().OnlyContain(reference => reference!.Joinable && !reference.Inferred);
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

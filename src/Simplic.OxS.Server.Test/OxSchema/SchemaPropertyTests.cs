using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>Kinds, nullability, pointers and the two naming-exception members.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaPropertyTests
    {
        [Fact]
        public void Build_Properties_CarryCamelCaseWireNames()
        {
            var names = SchemaBuild.Degraded.Document.PropertyNames("probe.widget");

            names.Should().Equal(
                "id", "isDeleted", "label", "qrCode", "externalReference", "caption", "slots", "tags");
        }

        [Theory]
        [InlineData("id", OxSchemaKinds.Guid, false)]
        [InlineData("isDeleted", OxSchemaKinds.Bool, false)]
        [InlineData("label", OxSchemaKinds.String, false)]
        [InlineData("qrCode", OxSchemaKinds.String, true)]
        [InlineData("caption", OxSchemaKinds.String, true)]
        public void Build_ScalarProperty_CarriesItsKindAndNullability(string name, string kind, bool nullable)
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.widget", name);

            property.Kind.Should().Be(kind);
            property.Nullable.Should().Be(nullable);
        }

        [Fact]
        public void Build_PropertyWithoutASetter_IsDescribedAsAPlainMember()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.widget", "label");

            property.Kind.Should().Be(OxSchemaKinds.String);
            property.References.Should().BeNull();
            property.SnapshotOf.Should().BeNull();
        }

        [Fact]
        public void Build_AcronymProperty_PublishesBothNamingExceptions()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.widget", "qrCode");

            property.StorageName.Should().Be("QRCode");
            property.DisplayName.Should().Be("QR Code");
        }

        [Fact]
        public void Build_DerivableNames_PublishNeitherNamingException()
        {
            var document = SchemaBuild.Degraded.Document;

            foreach (var name in new[] { "id", "isDeleted", "label", "externalReference", "caption" })
            {
                var property = document.Property("probe.widget", name);

                property.StorageName.Should().BeNull();
                property.DisplayName.Should().BeNull();
            }
        }

        [Fact]
        public void Build_NullableEnum_PointsAtTheSamePooledEnumAsItsNonNullableSibling()
        {
            var document = SchemaBuild.Degraded.Document;

            var required = document.Property("probe.thing", "mode");
            var optional = document.Property("probe.thing", "optionalMode");

            required.Kind.Should().Be(OxSchemaKinds.Enum);
            required.Target().Should().Be("t_mode");
            required.Nullable.Should().BeFalse();

            optional.Kind.Should().Be(OxSchemaKinds.Enum);
            optional.Target().Should().Be("t_mode");
            optional.Nullable.Should().BeTrue();
        }

        [Fact]
        public void Build_EnumInsideACollection_CarriesThePointerOnTheElement()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.thing", "modes");

            property.Kind.Should().Be(OxSchemaKinds.Array);
            property.Nullable.Should().BeFalse();
            property.Type.Should().BeNull();
            property.Of.Should().NotBeNull();
            property.Of!.Kind.Should().Be(OxSchemaKinds.Enum);
            property.Of.Target().Should().Be("t_mode");
        }

        [Fact]
        public void Build_ArrayOfObjects_PointsAtTheElementType()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.widget", "slots");

            property.Kind.Should().Be(OxSchemaKinds.Array);
            property.Of!.Kind.Should().Be(OxSchemaKinds.Object);
            property.Of.Target().Should().Be("t_slot");
        }

        [Fact]
        public void Build_Dictionary_DescribesItsValue()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.link", "keyed");

            property.Kind.Should().Be(OxSchemaKinds.Dictionary);
            property.Of.Should().BeNull();
            property.Value.Should().NotBeNull();
            property.Value!.Kind.Should().Be(OxSchemaKinds.Object);
            property.Value.Target().Should().Be("probe.thing");
        }

        [Fact]
        public void Build_NestedDescriptors_NameNoMember()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Property("probe.link", "many").Of!.Name.Should().BeNull();
            document.Property("probe.link", "keyed").Value!.Name.Should().BeNull();
            document.Property("probe.link", "many").Of!.Nullable.Should().BeNull();
        }

        [Fact]
        public void Build_ObjectProperty_PointsAtTheEntityRatherThanASecondStructuralEntry()
        {
            var property = SchemaBuild.Degraded.Document.Property("probe.link", "single");

            property.Kind.Should().Be(OxSchemaKinds.Object);
            property.Target().Should().Be("probe.thing");
        }

        [Fact]
        public void Build_IntegerProperty_IsDescribedAsAnInt()
        {
            var document = SchemaBuild.Degraded.Document;

            var pair = document.Types
                .First(entry => entry.Key.StartsWith("t_pair_", StringComparison.Ordinal)
                    && (entry.Value.Properties ?? []).Any(property => property.Kind == OxSchemaKinds.Int));

            document.Property(pair.Key, "first").Kind.Should().Be(OxSchemaKinds.Int);
            document.Property(pair.Key, "first").Nullable.Should().BeFalse();
            document.Property(pair.Key, "second").Kind.Should().Be(OxSchemaKinds.Int);
        }

        [Fact]
        public void Build_GenericClosures_AreDescribedWithTheirOwnArguments()
        {
            var document = SchemaBuild.Degraded.Document;

            var kinds = document.Types
                .Where(entry => entry.Key.StartsWith("t_pair_", StringComparison.Ordinal))
                .Select(entry => entry.Value.Properties!.Single(property => property.Name == "first").Kind);

            kinds.Should().BeEquivalentTo([OxSchemaKinds.Int, OxSchemaKinds.String]);
        }

        [Fact]
        public void Build_SameNamedNestedTypes_KeepSeparateDescriptions()
        {
            var document = SchemaBuild.Degraded.Document;

            var left = document.Property("probe.link", "left").Target()!;
            var right = document.Property("probe.link", "right").Target()!;

            left.Should().NotBe(right);
            document.PropertyNames(left).Should().Equal("text");
            document.PropertyNames(right).Should().Equal("number");
        }
    }
}

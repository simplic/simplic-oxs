using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The pooled enum entries: their value lists, flags and retirement marker.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaEnumTests
    {
        [Fact]
        public void Build_EnumEntry_IsMarkedAndCarriesNoPropertyList()
        {
            var entry = SchemaBuild.Degraded.Document.Entry("t_mode");

            entry.Kind.Should().Be(OxSchemaKinds.Enum);
            entry.Properties.Should().BeNull();
        }

        [Fact]
        public void Build_PlainEnum_PublishesItsMembersInDeclarationOrder()
        {
            var entry = SchemaBuild.Degraded.Document.Entry("t_mode");

            entry.Flags.Should().BeFalse();
            entry.Values!.Select(value => value.Name).Should().Equal("First", "Second");
            entry.Values.Select(value => value.Value).Should().Equal(0L, 1L);
            entry.Values.Should().OnlyContain(value => value.Active);
        }

        [Fact]
        public void Build_FlagsEnum_IsMarkedAndKeepsItsCombinedMember()
        {
            var entry = SchemaBuild.Degraded.Document.Entry("t_access");

            entry.Flags.Should().BeTrue();
            entry.Values!.Select(value => value.Name).Should().Equal("None", "Read", "Write", "ReadWrite");
            entry.Values.Select(value => value.Value).Should().Equal(0L, 1L, 2L, 3L);
        }

        [Fact]
        public void Build_ObsoleteEnumMember_IsPublishedAsInactive()
        {
            var entry = SchemaBuild.Degraded.Document.Entry("t_retired");

            entry.Values!.Single(value => value.Name == "Live").Active.Should().BeTrue();
            entry.Values.Single(value => value.Name == "Dead").Active.Should().BeFalse();
        }

        [Fact]
        public void Build_UnsignedMemberAboveTheSignedRange_WrapsRatherThanFailingTheBuild()
        {
            var entry = SchemaBuild.Degraded.Document.Entry("t_wide");

            entry.Flags.Should().BeFalse();
            entry.Values!.Single(value => value.Name == "Low").Value.Should().Be(1L);
            entry.Values.Single(value => value.Name == "Top").Value.Should().Be(long.MinValue);
        }

        [Fact]
        public void Build_EnumMemberNames_AreTheClrNamesVerbatim()
        {
            var document = SchemaBuild.Degraded.Document;

            var names = document.Types
                .Where(entry => entry.Value.Kind == OxSchemaKinds.Enum)
                .SelectMany(entry => entry.Value.Values ?? [])
                .Select(value => value.Name);

            names.Should().OnlyContain(name => char.IsUpper(name[0]));
        }

        [Fact]
        public void Build_EveryPooledEnum_CarriesAValueList()
        {
            var document = SchemaBuild.Degraded.Document;

            var enums = document.Types.Where(entry => entry.Value.Kind == OxSchemaKinds.Enum).ToList();

            enums.Select(entry => entry.Key)
                .Should().BeEquivalentTo("t_access", "t_mode", "t_retired", "t_umlaut", "t_wide");

            enums.Should().OnlyContain(entry => entry.Value.Values != null && entry.Value.Values.Count > 0);
        }
    }
}

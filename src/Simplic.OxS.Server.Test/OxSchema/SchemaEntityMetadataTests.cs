namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>Key, display, label, capabilities, aliases and item collections.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaEntityMetadataTests
    {
        [Fact]
        public void Build_EntityWithADeclaredIdentity_KeysOnIt()
        {
            SchemaBuild.Degraded.Document.Entry("probe.widget").Key.Should().Equal("id");
        }

        [Fact]
        public void Build_EntityWithoutADeclaredIdentity_PublishesNoKey()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("probe.thing").Key.Should().BeNull();
            document.PropertyNames("probe.thing").Should().Contain("id");
        }

        [Fact]
        public void Build_EmbeddedItemType_KeysOnItsDeclaredIdentity()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("t_slot").Key.Should().Equal("id");
            document.Entry("t_tag").Key.Should().BeNull();
            document.Entry("t_thingSubset").Key.Should().BeNull();
        }

        [Theory]
        [InlineData("badid", "Bad Id")]
        [InlineData("spare.gadget", "Spare Gadget")]
        [InlineData("probe.gadget", "Gadget")]
        [InlineData("probe.thing", "Thing")]
        [InlineData("probe.widget", "Widget")]
        public void Build_EntityLabel_IsTheHumanisedClrNameWithoutItsDtoSuffix(string id, string label)
        {
            SchemaBuild.Degraded.Document.Entry(id).DisplayName.Should().Be(label);
        }

        [Fact]
        public void Build_EntityWithANameProperty_PublishesItAsTheDisplayPath()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("badid").Display.Should().Be("name");
            document.Entry("probe.gadget").Display.Should().Be("name");
            document.Entry("probe.link").Display.Should().Be("name");
        }

        [Fact]
        public void Build_EntityWithNoDisplayCandidate_PublishesNone()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("probe.widget").Display.Should().BeNull();
            document.Entry("probe.thing").Display.Should().BeNull();
        }

        [Fact]
        public void Build_Extendable_ComesFromTheDeclarationAndQueryableIsAlwaysTrue()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("probe.widget").Extendable.Should().BeTrue();
            document.Entry("probe.thing").Extendable.Should().BeFalse();

            document.Types
                .Where(entry => entry.Value.Entity == true)
                .Should().OnlyContain(entry => entry.Value.Queryable == true);
        }

        [Fact]
        public void Build_CapabilityExceptions_ArePresentAndEmpty()
        {
            var document = SchemaBuild.Degraded.Document;

            foreach (var (_, entry) in document.Types.Where(entry => entry.Value.Entity == true))
            {
                entry.NotFilterable.Should().NotBeNull().And.BeEmpty();
                entry.NotSortable.Should().NotBeNull().And.BeEmpty();
            }
        }

        [Fact]
        public void Build_Items_ListEveryPathReachingACollectionOfAKeyedItemType()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("probe.widget").Items!.Select(item => item.Path).Should().Equal("slots");
            document.Entry("probe.thing").Items!.Select(item => item.Path).Should().Equal("slots");
            document.Entry("probe.link").Items!.Select(item => item.Path).Should().Equal("slots");
        }

        [Fact]
        public void Build_Items_ArePresentAndEmptyWhenTheEntityHasNone()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("badid").Items.Should().NotBeNull().And.BeEmpty();
            document.Entry("probe.gadget").Items.Should().NotBeNull().And.BeEmpty();
            document.Entry("spare.gadget").Items.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void Build_ItemAliases_AreOnlyPublishedWhenBothHalvesAreLegacyIds()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("probe.thing").Items!.Single().Aliases.Should().Equal("$ThingModel.$Slot");
            document.Entry("probe.widget").Items!.Single().Aliases.Should().BeEmpty();
            document.Entry("probe.link").Items!.Single().Aliases.Should().BeEmpty();
        }

        [Fact]
        public void Build_Aliases_CarryTheLinkedControllerDtoName()
        {
            SchemaBuild.Degraded.Document.Entry("probe.thing").Aliases.Should().Equal("$ThingModel");
        }

        [Fact]
        public void Build_Aliases_ArePresentAndEmptyWithoutAResolvedLink()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("probe.widget").Aliases.Should().NotBeNull().And.BeEmpty();
            document.Entry("probe.gadget").Aliases.Should().NotBeNull().And.BeEmpty();
            document.Entry("probe.link").Aliases.Should().NotBeNull().And.BeEmpty();
        }
    }
}

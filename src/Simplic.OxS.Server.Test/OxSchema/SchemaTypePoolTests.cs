using System.Text.RegularExpressions;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>Which keys the pool carries, and how the structural ones are minted.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaTypePoolTests
    {
        /// <summary>A structural pool key: the prefix plus one camelCase segment.</summary>
        private static readonly Regex StructuralId = new(@"^t_[a-z][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant);

        /// <summary>A structural pool key that carries a disambiguating tail.</summary>
        private static readonly Regex WithTail = new(@"^t_[a-z][a-zA-Z0-9]*_[0-9a-f]{6}$", RegexOptions.CultureInvariant);

        [Fact]
        public void Build_Pool_KeysEveryDescribedEntityByItsDeclaredId()
        {
            var document = SchemaBuild.Degraded.Document;

            var entities = document.Types
                .Where(entry => entry.Value.Entity == true)
                .Select(entry => entry.Key);

            entities.Should().BeEquivalentTo(
                "badid", "probe.bag", "probe.base", "probe.gadget", "probe.link", "probe.thing", "probe.widget", "spare.gadget");
        }

        [Fact]
        public void Build_Pool_CarriesOneEntryPerReachableStructuralType()
        {
            var document = SchemaBuild.Degraded.Document;

            var structural = document.Types
                .Where(entry => entry.Value.Entity != true)
                .Select(entry => entry.Key)
                .ToList();

            structural.Should().HaveCount(12);

            structural.Should().Contain(
                ["t_access", "t_mode", "t_retired", "t_wide", "t_umlaut", "t_slot", "t_tag", "t_thingSubset"]);

            structural.Where(id => id.StartsWith("t_detail_", StringComparison.Ordinal)).Should().HaveCount(2);
            structural.Where(id => id.StartsWith("t_pair_", StringComparison.Ordinal)).Should().HaveCount(2);
        }

        [Fact]
        public void Build_Pool_HasNoOtherEntries()
        {
            SchemaBuild.Degraded.Document.Types.Should().HaveCount(20);
        }

        [Fact]
        public void Build_StructuralIds_MatchTheMintedGrammar()
        {
            var document = SchemaBuild.Degraded.Document;

            foreach (var (id, entry) in document.Types.Where(entry => entry.Value.Entity != true))
            {
                StructuralId.IsMatch(id).Should().BeTrue($"{id} is a minted pool key");
                entry.Should().NotBeNull();
            }
        }

        [Fact]
        public void Build_UncontestedClrName_KeepsTheBareReadableId()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Types.Keys.Should().Contain("t_slot");
            document.Types.Keys.Should().Contain("t_thingSubset");
            document.Types.Keys.Should().NotContain(id => id.StartsWith("t_slot_", StringComparison.Ordinal));
        }

        [Fact]
        public void Build_ContestedClrName_GivesEveryClaimantATail()
        {
            var document = SchemaBuild.Degraded.Document;

            var contested = document.Types.Keys
                .Where(id => id.StartsWith("t_detail", StringComparison.Ordinal)
                    || id.StartsWith("t_pair", StringComparison.Ordinal))
                .ToList();

            contested.Should().HaveCount(4);
            contested.Should().OnlyContain(id => WithTail.IsMatch(id));
            contested.Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void Build_StructuralIds_AreIdenticalAcrossTwoBuilds()
        {
            var first = SchemaBuild.Build().Document.Types.Keys.ToList();
            var second = SchemaBuild.Build().Document.Types.Keys.ToList();

            second.Should().Equal(first);
        }

        [Fact]
        public void Build_DuplicatedEntityId_IsAbsentFromThePool()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Types.Keys.Should().NotContain("probe.twin");
            document.Types.Keys.Should().NotContain("Probe.Twin");
            document.Types.Keys.Should().NotContain(id => id.StartsWith("t_twin", StringComparison.Ordinal));
        }

        [Fact]
        public void Build_Entities_AreMarkedAndStructuralTypesAreNot()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("probe.widget").Entity.Should().BeTrue();
            document.Entry("t_slot").Entity.Should().BeFalse();
            document.Entry("t_mode").Entity.Should().BeFalse();
        }

        [Fact]
        public void Build_StructuralTypes_CarryNoEntityMetadata()
        {
            var entry = SchemaBuild.Degraded.Document.Entry("t_tag");

            entry.DisplayName.Should().BeNull();
            entry.Aliases.Should().BeNull();
            entry.Display.Should().BeNull();
            entry.Extendable.Should().BeNull();
            entry.Queryable.Should().BeFalse();
            entry.Items.Should().BeNull();
            entry.Operations.Should().BeNull();
        }

        [Fact]
        public void Build_EveryPointer_ResolvesToAPoolEntry()
        {
            var document = SchemaBuild.Degraded.Document;

            foreach (var (_, entry) in document.Types)
                foreach (var property in entry.Properties ?? [])
                    foreach (var pointer in Pointers(property))
                    {
                        pointer.Should().StartWith(OxSchemaPointer.Prefix);
                        document.Types.Keys.Should().Contain(OxSchemaPointer.Strip(pointer));
                    }
        }

        private static IEnumerable<string> Pointers(OxSchemaProperty descriptor)
        {
            if (descriptor.Type is not null)
                yield return descriptor.Type;

            if (descriptor.Of is not null)
                foreach (var pointer in Pointers(descriptor.Of))
                    yield return pointer;

            if (descriptor.Value is not null)
                foreach (var pointer in Pointers(descriptor.Value))
                    yield return pointer;
        }
    }
}

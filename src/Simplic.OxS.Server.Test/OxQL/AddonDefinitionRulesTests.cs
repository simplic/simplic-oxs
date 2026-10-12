using OxQL.Model.Addon;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.Test.OxSchema;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>What a definition must satisfy: the entity, the path, the kind, the value list and no shadowing.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class AddonDefinitionRulesTests
    {
        private static AddonDefinitionDocument Existing(string path, string kind, bool retired = false) =>
            new() { Entity = "probe.widget", Path = path, Kind = kind, Retired = retired };

        [Fact]
        public void CheckEntity_AcceptsAnExtendableEntityOfTheHost()
        {
            AddonDefinitionRules.CheckEntity(SchemaBuild.Degraded.Model, "probe.widget").Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("probe.nothing")]
        [InlineData("Probe.Widget")]
        public void CheckEntity_RefusesAnUnknownEntity(string? entity)
        {
            AddonDefinitionRules.CheckEntity(SchemaBuild.Degraded.Model, entity).Should().NotBeNull();
        }

        [Fact]
        public void CheckEntity_RefusesAnEntityThatIsNotExtendable()
        {
            AddonDefinitionRules.CheckEntity(SchemaBuild.Degraded.Model, "probe.thing").Should().Contain("not extendable");
        }

        /// <summary>
        /// The flag is not enough: an entity declared extendable with no <c>addon</c> member
        /// has no addon root in the path index, so a definition stored on it would be
        /// published under <c>/schema/addons</c> and refused with <c>UNKNOWN_PATH</c> on every
        /// read. A service must not publish a key it can never answer.
        /// </summary>
        [Fact]
        public void CheckEntity_RefusesAnExtendableEntityThatCarriesNoAddonBag()
        {
            AddonDefinitionRules.CheckEntity(SchemaBuild.Degraded.Model, "probe.bagless").Should().Contain("no 'addon' member");
        }

        [Fact]
        public void HasBag_DistinguishesTheBagMemberFromTheExtendableFlag()
        {
            var model = SchemaBuild.Degraded.Model;

            model.Entities["probe.bagless"].Extendable.Should().BeTrue();
            AddonDefinitionRules.HasBag(model.Entities["probe.bagless"]).Should().BeFalse();
            AddonDefinitionRules.HasBag(model.Entities["probe.widget"]).Should().BeTrue();
        }

        [Theory]
        [InlineData("weight")]
        [InlineData("vincario._v.data")]
        [InlineData("Ablieferbelege vorhanden")]
        [InlineData("a.b.c")]
        public void CheckPath_AcceptsStoragePathsVerbatim(string path)
        {
            AddonDefinitionRules.CheckPath(path).Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("a..b")]
        [InlineData(".a")]
        [InlineData("a.")]
        [InlineData("$set")]
        [InlineData("a.$v")]
        [InlineData(" weight")]
        [InlineData("weight ")]
        public void CheckPath_RefusesEmptySegmentsDollarsAndEdgeWhitespace(string? path)
        {
            AddonDefinitionRules.CheckPath(path).Should().NotBeNull();
        }

        [Fact]
        public void CheckPath_BoundsTheLengthAndTheSegments()
        {
            AddonDefinitionRules.CheckPath(new string('a', AddonDefinitionRules.MaxPathLength)).Should().BeNull();
            AddonDefinitionRules.CheckPath(new string('a', AddonDefinitionRules.MaxPathLength + 1)).Should().Contain("longer than");

            AddonDefinitionRules.CheckPath(string.Join('.', Enumerable.Repeat("a", AddonDefinitionRules.MaxPathSegments))).Should().BeNull();
            AddonDefinitionRules.CheckPath(string.Join('.', Enumerable.Repeat("a", AddonDefinitionRules.MaxPathSegments + 1))).Should().Contain("segments");
        }

        [Fact]
        public void CheckValues_BoundsTheListAndItsTexts()
        {
            static List<AddonDefinitionValue> Values(int count) => [.. Enumerable.Range(0, count).Select(index => new AddonDefinitionValue { Value = index.ToString(System.Globalization.CultureInfo.InvariantCulture) })];

            var longest = new string('a', AddonDefinitionRules.MaxLabelLength);

            AddonDefinitionRules.CheckValues(AddonKind.Int, Values(AddonDefinitionRules.MaxValues)).Should().BeNull();
            AddonDefinitionRules.CheckValues(AddonKind.Int, Values(AddonDefinitionRules.MaxValues + 1)).Should().Contain("at most");

            AddonDefinitionRules.CheckValues(AddonKind.String, [new() { Value = longest, Label = longest }]).Should().BeNull();
            AddonDefinitionRules.CheckValues(AddonKind.String, [new() { Value = longest + "a" }]).Should().Contain("characters");
            AddonDefinitionRules.CheckValues(AddonKind.String, [new() { Value = "a", Label = longest + "a" }]).Should().Contain("characters");
        }

        [Fact]
        public void CheckTexts_BoundsTheDisplayNameAndTheDescription()
        {
            var name = new string('a', AddonDefinitionRules.MaxLabelLength);
            var description = new string('a', AddonDefinitionRules.MaxDescriptionLength);

            AddonDefinitionRules.CheckTexts(null, null).Should().BeNull();
            AddonDefinitionRules.CheckTexts(name, description).Should().BeNull();
            AddonDefinitionRules.CheckTexts(name + "a", description).Should().Contain("display name");
            AddonDefinitionRules.CheckTexts(name, description + "a").Should().Contain("description");
        }

        [Fact]
        public void CheckCount_BoundsTheDefinitionsOfAnEntityThatAreNotRetired()
        {
            var full = Enumerable.Range(0, AddonDefinitionRules.MaxDefinitionsPerEntity).Select(index => Existing($"key{index}", "string")).ToList();

            AddonDefinitionRules.CheckCount("probe.widget", full.Skip(1)).Should().BeNull();
            AddonDefinitionRules.CheckCount("probe.widget", full.Skip(1).Append(Existing("old", "string", retired: true))).Should().BeNull();
            AddonDefinitionRules.CheckCount("probe.widget", full).Should().Contain("retire one first");
        }

        [Theory]
        [InlineData("string", AddonKind.String)]
        [InlineData("int", AddonKind.Int)]
        [InlineData("long", AddonKind.Long)]
        [InlineData("double", AddonKind.Double)]
        [InlineData("decimal", AddonKind.Decimal)]
        [InlineData("bool", AddonKind.Bool)]
        [InlineData("date", AddonKind.Date)]
        [InlineData("dateTime", AddonKind.DateTime)]
        [InlineData("guid", AddonKind.Guid)]
        [InlineData("object", AddonKind.Object)]
        public void ParseKind_ReadsTheSchemasSpellingsExactly(string name, AddonKind kind)
        {
            AddonDefinitionRules.ParseKind(name).Should().Be(kind);
            AddonDefinitionRules.KindName(kind).Should().Be(name);
            AddonDefinitionRules.CheckKind(name).Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("String")]
        [InlineData("datetime")]
        [InlineData("array")]
        [InlineData("unknown")]
        public void CheckKind_RefusesEverythingElse(string? name)
        {
            AddonDefinitionRules.ParseKind(name).Should().BeNull();
            AddonDefinitionRules.CheckKind(name).Should().NotBeNull();
        }

        [Fact]
        public void CheckValues_AllowsALabelledListOnStringAndIntKeys()
        {
            List<AddonDefinitionValue> values = [new() { Value = "1", Label = "One" }, new() { Value = "2" }];

            AddonDefinitionRules.CheckValues(AddonKind.String, values).Should().BeNull();
            AddonDefinitionRules.CheckValues(AddonKind.Int, values).Should().BeNull();
            AddonDefinitionRules.CheckValues(AddonKind.Decimal, null).Should().BeNull();
            AddonDefinitionRules.CheckValues(AddonKind.Decimal, []).Should().BeNull();
        }

        [Fact]
        public void CheckValues_RefusesAListOnOtherKindsAndNonIntegersOnInt()
        {
            List<AddonDefinitionValue> values = [new() { Value = "1" }];

            AddonDefinitionRules.CheckValues(AddonKind.Decimal, values).Should().Contain("string and int");
            AddonDefinitionRules.CheckValues(AddonKind.Int, [new() { Value = "1.5" }]).Should().Contain("not an integer");
            AddonDefinitionRules.CheckValues(AddonKind.String, [new() { Value = "" }]).Should().Contain("non-empty");
        }

        [Fact]
        public void CheckShadowing_AllowsSiblingsAndChildrenOfAnObject()
        {
            var existing = new[] { Existing("vincario", "object"), Existing("weight", "decimal") };

            AddonDefinitionRules.CheckShadowing("vincario.data", AddonKind.String, existing).Should().BeNull();
            AddonDefinitionRules.CheckShadowing("colour", AddonKind.String, existing).Should().BeNull();
            AddonDefinitionRules.CheckShadowing("other", AddonKind.Object, existing).Should().BeNull();
        }

        [Fact]
        public void CheckShadowing_RefusesADescendantOfAScalar()
        {
            AddonDefinitionRules.CheckShadowing("weight.unit", AddonKind.String, [Existing("weight", "decimal")])
                .Should().Contain("cannot have defined descendants");
        }

        [Fact]
        public void CheckShadowing_RefusesAScalarAboveADefinedKey()
        {
            AddonDefinitionRules.CheckShadowing("vincario", AddonKind.String, [Existing("vincario._v.data", "string")])
                .Should().Contain("would shadow");
            AddonDefinitionRules.CheckShadowing("vincario", AddonKind.Object, [Existing("vincario._v.data", "string")])
                .Should().BeNull();
        }

        [Fact]
        public void CheckShadowing_IgnoresRetiredDefinitionsAndThePathItself()
        {
            AddonDefinitionRules.CheckShadowing("weight.unit", AddonKind.String, [Existing("weight", "decimal", retired: true)]).Should().BeNull();
            AddonDefinitionRules.CheckShadowing("weight", AddonKind.Decimal, [Existing("weight", "decimal")]).Should().BeNull();
        }

        [Fact]
        public void CheckShadowing_DoesNotConfuseAPrefixWithAnAncestor()
        {
            AddonDefinitionRules.CheckShadowing("weightUnit", AddonKind.String, [Existing("weight", "decimal")]).Should().BeNull();
        }
    }
}

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
        /// F-COR-002: the flag alone was the test, so <c>erp.transaction</c> and
        /// <c>vehicle.equipment</c> — extendable with no <c>addon</c> member — accepted a
        /// definition (201), published it under <c>/schema/addons</c>, and then refused every
        /// read of it with <c>UNKNOWN_PATH</c>, because with no bag the path index has no
        /// addon root to bind. A service must not publish a key it can never answer.
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

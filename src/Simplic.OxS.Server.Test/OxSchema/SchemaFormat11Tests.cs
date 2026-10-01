using System.Text.Json;
using OxQL.Core.Models;
using OxQL.Model;
using OxQL.Model.Attributes;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Test.OxSchema.Fixtures;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>
    /// The format 1.1 members: descriptions, constraints and deprecations, polymorphic types,
    /// reference cases, the host-side reference declarations and the added limits.
    /// </summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaFormat11Tests
    {
        private static OxSchemaOptionsBuilder Builder() => new()
        {
            ServiceName = SchemaBuild.Service,
            ApiName = "probe-api",
            ApiVersion = "v1",
            TypeAssemblies = SchemaBuild.Options().TypeAssemblies,
            ControllerTypes = SchemaBuild.Options().ControllerTypes,
        };

        /// <summary>The canonical bytes of one property of the served body.</summary>
        private static string RawProperty(OxSchemaRegistry registry, string id, string name)
        {
            using var body = JsonDocument.Parse(registry.Body);

            return body.RootElement.GetProperty("types").GetProperty(id).GetProperty("properties")
                .EnumerateArray()
                .Single(property => property.GetProperty("name").GetString() == name)
                .GetRawText();
        }

        /// <summary>The canonical bytes of one pool entry of the served body.</summary>
        private static JsonElement RawEntry(JsonDocument body, string id) =>
            body.RootElement.GetProperty("types").GetProperty(id);

        [Fact]
        public void Build_SchemaVersion_IsTheCurrentFormat()
        {
            OxSchemaDocument.CurrentSchemaVersion.Should().Be("1.1");
            SchemaBuild.Degraded.Document.SchemaVersion.Should().Be(OxSchemaDocument.CurrentSchemaVersion);
        }

        [Fact]
        public void Build_Limits_PublishTheFormat11LimitsOfTheQueryEngine()
        {
            var limits = new OxQLOptions { Limits = { MaxContinuedStages = 3, MaxFlattenDepth = 2, MaxReportPageSize = 777, MaxResolveStages = 6, MaxLookupLimit = 40 } };

            var published = SchemaBuild.Build(SchemaBuild.Options() with { QueryLimits = limits }).Document.Limits;

            published.MaxContinuedStages.Should().Be(3);
            published.MaxFlattenDepth.Should().Be(2);
            published.MaxReportPageSize.Should().Be(777);
            published.MaxResolveStages.Should().Be(6);
            published.MaxLookupLimit.Should().Be(40);
        }

        [Fact]
        public void Build_Limits_AppendTheFormat11LimitsAfterEveryFormat10Limit()
        {
            using var body = JsonDocument.Parse(SchemaBuild.Degraded.Body);

            body.RootElement.GetProperty("limits").EnumerateObject().Select(member => member.Name).Should().Equal(
                "maxPageSize", "defaultPageSize", "maxPipelineStages", "maxLookupStages", "maxUnwindStages",
                "maxGroupFields", "maxProjectionFields", "regexMaxLength", "maxOffset", "maxResolveStages",
                "maxBatchQueries", "maxLookupLimit", "maxContinuedStages", "maxFlattenDepth", "maxReportPageSize");
        }

        [Fact]
        public void Build_Descriptions_ArePublishedOnEntitiesPropertiesEnumsAndEnumValues()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("probe.ledger").Description.Should().Be("A ledger of entries.");
            document.Property("probe.ledger", "name").Description.Should().Be("The ledger's name.");
            document.Entry("t_ledgerKind").Description.Should().Be("How a ledger is kept.");
            document.Entry("t_ledgerKind").Values!.Select(value => value.Description).Should().Equal("A cash book.", null);
            document.Property("probe.ledger", "rank").Description.Should().BeNull();
        }

        [Fact]
        public void Build_ConstraintsAndDeprecation_AreTheModelsOwn()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Property("probe.ledger", "name").Constraints.Should().Be(new OxSchemaConstraints { MaxLength = 40 });
            document.Property("probe.ledger", "rank").Constraints.Should().Be(new OxSchemaConstraints { Min = "1", Max = "10" });
            document.Property("probe.ledger", "code").Constraints.Should().Be(new OxSchemaConstraints { Pattern = "^[A-Z]+$" });
            document.Property("probe.ledger", "title").Deprecated.Should().Be(new OxSchemaDeprecation { Note = "Use name." });
            document.Property("probe.ledger", "title").Constraints.Should().BeNull();
        }

        [Fact]
        public void Build_Property_EmitsTheFormat11MembersInTheirDeclaredOrder()
        {
            var registry = SchemaBuild.Degraded;

            RawProperty(registry, "probe.ledger", "name").Should().Be(
                "{\"name\":\"name\",\"kind\":\"string\",\"nullable\":true,\"description\":\"The ledger\\u0027s name.\",\"constraints\":{\"maxLength\":40}}");
            RawProperty(registry, "probe.ledger", "title").Should().Be(
                "{\"name\":\"title\",\"kind\":\"string\",\"nullable\":true,\"deprecated\":{\"note\":\"Use name.\"}}");
            RawProperty(registry, "t_entry", "items").Should().Be(
                "{\"name\":\"items\",\"kind\":\"array\",\"of\":{\"kind\":\"object\",\"type\":\"#/types/t_entry\"},\"nullable\":true,\"onlyFor\":[\"GroupEntry\"]}");
        }

        [Fact]
        public void Build_ReferenceCases_EmitWhenKeyAsTargetsAndEntityItemFieldInThatOrder()
        {
            var registry = SchemaBuild.Degraded;

            RawProperty(registry, "t_sourceReference", "id").Should().Be(
                "{\"name\":\"id\",\"kind\":\"guid\",\"nullable\":false,\"referenceCases\":[" +
                "{\"when\":{\"path\":\"type\",\"equals\":[\"widget\"]},\"targets\":[{\"entity\":\"probe.widget\",\"item\":\"slots\",\"field\":\"id\"}]}," +
                "{\"when\":{\"path\":\"type\",\"equals\":[\"thing\"]},\"targets\":[{\"entity\":\"probe.thing\",\"field\":\"id\"}]}]}");
            RawProperty(registry, "probe.ledger", "referenceId").Should().Be(
                "{\"name\":\"referenceId\",\"kind\":\"string\",\"nullable\":true,\"referenceCases\":[" +
                "{\"when\":{\"path\":\"dataType\",\"equals\":[\"thing\"]},\"keyAs\":\"guid\",\"targets\":[{\"entity\":\"probe.thing\",\"field\":\"id\"}]}]}");
        }

        [Fact]
        public void Build_ItemReference_IsACaseAndNeverASimpleReference()
        {
            var slot = SchemaBuild.Degraded.Document.Property("probe.ledger", "slotId");

            slot.References.Should().BeNull();
            slot.ReferenceCases.Should().ContainSingle().Which.Should().BeEquivalentTo(new OxSchemaReferenceCase
            {
                Targets = [new OxSchemaReferenceTarget { Entity = "probe.widget", Item = "slots", Field = "id" }],
            });
        }

        [Fact]
        public void Build_EveryDescriptor_CarriesAtMostOneReferenceForm_AndReferencesOnlyTheSimpleOne()
        {
            foreach (var (id, entry) in SchemaBuild.Degraded.Document.Types)
                foreach (var property in entry.Properties ?? [])
                {
                    if (property.References is not null)
                        property.ReferenceCases.Should().BeNull($"{id}#{property.Name} is a simple reference");

                    property.ReferenceCases?.Should().NotBeEmpty($"{id}#{property.Name} publishes no empty case list");
                }
        }

        [Fact]
        public void Build_SimpleReference_KeepsTheFormat10Member()
        {
            var reference = SchemaBuild.Degraded.Document.Property("t_lineEntry", "thingId");

            reference.References.Should().Be(new OxSchemaReference { Entity = "probe.thing", Field = "id", Joinable = true, Inferred = false });
            reference.ReferenceCases.Should().BeNull();
        }

        [Fact]
        public void Build_PolymorphicType_PublishesItsDiscriminatorAndVariantsByName()
        {
            var entry = SchemaBuild.Degraded.Document.Entry("t_entry");

            entry.Discriminator.Should().Be(new OxSchemaDiscriminator { Element = "_t", Form = OxSchemaDiscriminatorForms.Scalar });
            entry.Variants.Should().Equal(
                new OxSchemaVariant { Name = "GroupEntry", Type = "#/types/t_groupEntry" },
                new OxSchemaVariant { Name = "LineEntry", Type = "#/types/t_lineEntry" });
        }

        [Fact]
        public void Build_PolymorphicType_MergesTheVariantsMembersAsNullableWithOnlyFor()
        {
            var document = SchemaBuild.Degraded.Document;

            document.PropertyNames("t_entry").Should().Equal("id", "note", "items", "thingId");
            document.Property("t_entry", "id").OnlyFor.Should().BeNull();
            document.Property("t_entry", "note").OnlyFor.Should().BeNull();
            document.Property("t_entry", "items").OnlyFor.Should().Equal("GroupEntry");
            document.Property("t_entry", "thingId").OnlyFor.Should().Equal("LineEntry");
            document.Property("t_entry", "thingId").Nullable.Should().BeTrue();
        }

        [Fact]
        public void Build_PolymorphicEntry_EmitsDiscriminatorAndVariantsAfterItsProperties()
        {
            using var body = JsonDocument.Parse(SchemaBuild.Degraded.Body);
            var entry = RawEntry(body, "t_entry");

            entry.EnumerateObject().Select(member => member.Name).Should().Equal("properties", "discriminator", "variants", "baseVariant");
            entry.GetProperty("discriminator").GetRawText().Should().Be("{\"element\":\"_t\",\"form\":\"scalar\"}");
            entry.GetProperty("variants").GetRawText().Should().Be(
                "[{\"name\":\"GroupEntry\",\"type\":\"#/types/t_groupEntry\"},{\"name\":\"LineEntry\",\"type\":\"#/types/t_lineEntry\"}]");
        }

        [Fact]
        public void Build_PolymorphicType_NamesItsConcreteBaseAsTheVariantOfItsOwnValues()
        {
            var document = SchemaBuild.Degraded.Document;

            // Entry is a concrete class: a value stored as an Entry itself is one `is` can name.
            document.Entry("t_entry").BaseVariant.Should().Be("Entry");

            foreach (var (id, entry) in document.Types.Where(entry => entry.Value.Variants is null))
                entry.BaseVariant.Should().BeNull(id);
        }

        [Fact]
        public void Build_UnstoredMember_SaysSo_AndAStoredOneSaysNothing()
        {
            var registry = SchemaBuild.Degraded;

            // `label` is in the wire view and has no storage: a query can project it and nothing else.
            registry.Document.Property("probe.widget", "label").Stored.Should().BeFalse();
            RawProperty(registry, "probe.widget", "label").Should().EndWith("\"stored\":false}");
            registry.Document.Property("probe.ledger", "name").Stored.Should().BeNull();
            RawProperty(registry, "probe.ledger", "name").Should().NotContain("stored");
        }

        [Fact]
        public void Build_StoredAs_SaysHowAValueIsStoredWhereItsKindDoesNot()
        {
            var registry = SchemaBuild.Degraded;

            RawProperty(registry, "probe.ledger", "grade").Should().Be("{\"name\":\"grade\",\"kind\":\"string\",\"nullable\":false,\"storedAs\":\"codePoint\"}");
            registry.Document.Property("probe.ledger", "tally").StoredAs.Should().Be(OxSchemaStoredAs.ArrayOfDocuments);
            registry.Document.Property("probe.ledger", "tally").Kind.Should().Be(OxSchemaKinds.Dictionary);
            registry.Document.Property("probe.ledger", "stamp").StoredAs.Should().Be(OxSchemaStoredAs.Document);
            registry.Document.Property("probe.ledger", "stamp").Kind.Should().Be(OxSchemaKinds.DateTime);

            // The representation a kind implies is not published.
            foreach (var name in new[] { "name", "rank", "kind", "slotId", "entries" })
                registry.Document.Property("probe.ledger", name).StoredAs.Should().BeNull(name);
        }

        [Fact]
        public void Build_VariantPointers_AllNameAPoolEntry()
        {
            var document = SchemaBuild.Degraded.Document;

            foreach (var (id, entry) in document.Types.Where(entry => entry.Value.Variants is not null))
                foreach (var variant in entry.Variants!)
                    document.Types.Should().ContainKey(OxSchemaPointer.Strip(variant.Type), $"{id} names variant {variant.Name}");
        }

        [Fact]
        public void Build_TypeWithoutVariants_CarriesNeitherDiscriminatorNorVariants()
        {
            var document = SchemaBuild.Degraded.Document;

            foreach (var id in new[] { "t_slot", "probe.ledger", "t_groupEntry", "t_ledgerKind" })
            {
                document.Entry(id).Discriminator.Should().BeNull(id);
                document.Entry(id).Variants.Should().BeNull(id);
            }
        }

        [Fact]
        public void Build_EnumValue_EmitsItsDescriptionLast()
        {
            using var body = JsonDocument.Parse(SchemaBuild.Degraded.Body);

            RawEntry(body, "t_ledgerKind").GetProperty("values")[0].GetRawText().Should().Be(
                "{\"name\":\"Cash\",\"value\":0,\"active\":true,\"description\":\"A cash book.\"}");
        }

        /// <summary>The string constants of a codes class.</summary>
        private static IReadOnlyList<string> CodesOf(Type codes) =>
            [.. codes.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!)];

        [Fact]
        public void Build_ModelCodesTheDocumentDoesNotShare_AreLogOnly()
        {
            var own = CodesOf(typeof(BuildCodes)).Except(CodesOf(typeof(OxSchemaCodes)), StringComparer.Ordinal).ToList();

            own.Should().Contain([BuildCodes.PolymorphicSubtypeUnregistered, BuildCodes.ReferenceTargetFieldUnknown, BuildCodes.RetiredIdAmbiguous, BuildCodes.ReferenceTargetUnknown]);
            own.Should().OnlyContain(code => !OxSchemaCodes.Refuses(code) && !OxSchemaCodes.IsPublished(code));

            var registry = SchemaBuild.Degraded;

            registry.Findings.Should().Contain(finding => finding.Code == BuildCodes.PolymorphicSubtypeUnregistered && finding.Target == "probe.base");
            registry.Document.Diagnostics!.Select(diagnostic => diagnostic.Code).Should().NotContain(own);
        }

        [Fact]
        public void Validate_AVariantPointerWithoutAPoolEntry_IsADanglingPointer()
        {
            var pool = new Dictionary<string, OxSchemaType>(StringComparer.Ordinal)
            {
                ["t_entry"] = new()
                {
                    Kind = "object",
                    Properties = [],
                    Discriminator = new OxSchemaDiscriminator { Element = "_t", Form = OxSchemaDiscriminatorForms.Scalar },
                    Variants =
                    [
                        new OxSchemaVariant { Name = "GroupEntry", Type = "#/types/t_groupEntry" },
                        new OxSchemaVariant { Name = "LineEntry", Type = "#/types/t_lineEntry" },
                    ],
                },
                ["t_lineEntry"] = new() { Kind = "object", Properties = [] },
            };
            var findings = new FindingCollector();

            DocumentValidator.Inspect(pool, findings);

            var finding = findings.Sorted().Should().ContainSingle().Subject;
            finding.Code.Should().Be(OxSchemaCodes.DanglingTypePointer);
            finding.Target.Should().Be("t_entry -> #/types/t_groupEntry");
            finding.Refuses.Should().BeTrue();
        }

        [Fact]
        public void DeclareReference_GivesASharedTypesMemberASimpleReference()
        {
            SchemaBuild.Degraded.Document.Property("t_party", "id").References.Should().BeNull();

            var options = Builder().DeclareReference<Party>("id", "probe.thing").Build();

            SchemaBuild.Build(options).Document.Property("t_party", "id").References
                .Should().Be(new OxSchemaReference { Entity = "probe.thing", Field = "id", Joinable = true, Inferred = false });
        }

        [Fact]
        public void DeclareReference_WithAnItem_PublishesACaseAndNoSimpleReference()
        {
            var options = Builder().DeclareReference<Party>("id", "probe.widget", "id", item: "slots").Build();

            var id = SchemaBuild.Build(options).Document.Property("t_party", "id");

            id.References.Should().BeNull();
            id.ReferenceCases.Should().ContainSingle().Which.Targets.Should().Equal(
                new OxSchemaReferenceTarget { Entity = "probe.widget", Item = "slots", Field = "id" });
        }

        [Fact]
        public void DeclareReferenceWhen_OnTheVariant_PublishesAVariantCondition()
        {
            var options = Builder()
                .DeclareReferenceWhen<Entry>("id", OxSchemaOptionsBuilder.Variant, "LineEntry", "id", "probe.widget#slots")
                .Build();

            var cases = SchemaBuild.Build(options).Document.Property("t_entry", "id").ReferenceCases;

            cases.Should().ContainSingle().Which.Should().BeEquivalentTo(new OxSchemaReferenceCase
            {
                When = new OxSchemaReferenceCondition { Variant = ["LineEntry"] },
                Targets = [new OxSchemaReferenceTarget { Entity = "probe.widget", Item = "slots", Field = "id" }],
            });
        }

        [Fact]
        public void DeclareReferenceWhen_WithKeyAs_PublishesTheConversion()
        {
            var options = Builder()
                .DeclareReferenceWhen<Party>("externalId", "externalKind", "thing", null, OxQLKeyAs.Guid, "probe.thing")
                .Build();

            var cases = SchemaBuild.Build(options).Document.Property("t_party", "externalId").ReferenceCases;

            cases.Should().ContainSingle().Which.Should().BeEquivalentTo(new OxSchemaReferenceCase
            {
                When = new OxSchemaReferenceCondition { Path = "externalKind", EqualsAny = ["thing"] },
                KeyAs = OxSchemaKeyAs.Guid,
                Targets = [new OxSchemaReferenceTarget { Entity = "probe.thing", Field = "id" }],
            });
        }

        [Fact]
        public void DeclareReference_OnAnAnnotatedMember_DropsBothAndLogsTheConflict()
        {
            var registry = SchemaBuild.Build(Builder().DeclareReference<LineEntry>("thingId", "probe.gadget").Build());

            registry.Document.Property("t_lineEntry", "thingId").References.Should().BeNull();
            registry.Document.Property("t_lineEntry", "thingId").ReferenceCases.Should().BeNull();

            var finding = registry.Findings.Single(finding => finding.Target == "t_lineEntry#thingId");

            finding.Code.Should().Be(OxSchemaCodes.ReferenceDeclarationUnresolved);
            finding.Published.Should().BeFalse();
            finding.Refuses.Should().BeFalse();
        }

        [Fact]
        public void DeclareReference_ToAnotherServiceWithoutAField_EmitsNoReferenceAndLogsTheMissingField()
        {
            var registry = SchemaBuild.Build(Builder().DeclareReference<Party>("id", "staff.employee").Build());

            registry.Document.Property("t_party", "id").References.Should().BeNull();
            registry.Document.Property("t_party", "id").ReferenceCases.Should().BeNull();

            var finding = registry.Findings.Single(finding => finding.Code == BuildCodes.ReferenceTargetFieldUnknown);

            finding.Target.Should().Be("t_party#id");
            finding.Published.Should().BeFalse();
            finding.Refuses.Should().BeFalse();
        }

        [Fact]
        public void DeclareReference_ToAnotherServiceWithAField_PublishesTheSimpleReference()
        {
            var registry = SchemaBuild.Build(Builder().DeclareReference<Party>("id", "staff.employee", field: "id").Build());

            registry.Document.Property("t_party", "id").References
                .Should().Be(new OxSchemaReference { Entity = "staff.employee", Field = "id", Joinable = true, Inferred = false });
            registry.Findings.Should().NotContain(finding => finding.Code == BuildCodes.ReferenceTargetFieldUnknown);
        }

        /// <summary>The findings of <paramref name="registry"/> the fixture host without declarations does not have.</summary>
        private static IReadOnlyList<OxSchemaFinding> Added(OxSchemaRegistry registry) =>
            [.. registry.Findings.Where(finding => !SchemaBuild.Degraded.Findings.Any(known => known.Code == finding.Code && known.Target == finding.Target))];

        [Fact]
        public void DeclareReference_OnAMisspeltWireMember_LogsTheDeclarationAndLeavesTheDocument()
        {
            var registry = SchemaBuild.Build(Builder().DeclareReference<Party>("idd", "probe.thing").Build());

            var finding = Added(registry).Should().ContainSingle().Subject;

            finding.Code.Should().Be(OxSchemaCodes.ReferenceDeclarationUnresolved);

            finding.Target.Should().Be("t_party#idd");
            finding.Published.Should().BeFalse();
            finding.Refuses.Should().BeFalse();
            registry.Revision.Should().Be(SchemaBuild.Degraded.Revision, "a declaration that resolves to nothing changes nothing published");
        }

        /// <summary>A type no entity of the fixture host embeds, so the model does not describe it.</summary>
        private sealed class Unpooled
        {
            public Guid Id { get; set; }
        }

        [Fact]
        public void DeclareReference_OnATypeTheModelDoesNotDescribe_LogsTheDeclarationAndLeavesTheDocument()
        {
            var registry = SchemaBuild.Build(Builder().DeclareReference<Unpooled>("id", "probe.thing").Build());

            var finding = Added(registry).Should().ContainSingle().Subject;

            finding.Code.Should().Be(OxSchemaCodes.ReferenceDeclarationUnresolved);

            finding.Published.Should().BeFalse();
            finding.Refuses.Should().BeFalse();
            registry.Revision.Should().Be(SchemaBuild.Degraded.Revision);
        }

        [Fact]
        public void DeclareReference_OnATypeAndItsVariant_KeepsTheTypesAndLogsTheVariantsConflict()
        {
            var registry = SchemaBuild.Build(Builder()
                .DeclareReference<Entry>("id", "probe.thing")
                .DeclareReference<LineEntry>("id", "probe.gadget")
                .Build());

            var finding = Added(registry).Should().ContainSingle().Subject;

            finding.Code.Should().Be(OxSchemaCodes.ReferenceDeclarationUnresolved);
            finding.Target.Should().Be("t_lineEntry#id");
            finding.Published.Should().BeFalse();
            finding.Refuses.Should().BeFalse();
            registry.Document.Property("t_entry", "id").References
                .Should().Be(new OxSchemaReference { Entity = "probe.thing", Field = "id", Joinable = true, Inferred = false });
            registry.Document.Property("t_lineEntry", "id").References.Should().BeNull("two declarations reach the variant's member, so it keeps neither");
            registry.Document.Property("t_lineEntry", "id").ReferenceCases.Should().BeNull();
        }

        [Theory]
        [InlineData("transport.shipment#billingLines")]
        [InlineData("probe.widget#slots")]
        public void DeclareReferenceWhen_WithATargetInTheFieldsPosition_Throws(string misplaced)
        {
            var declare = () => Builder().DeclareReferenceWhen<Party>("id", "externalKind", "logistics", misplaced, "transport.tour#billingLines");

            declare.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("field");
        }

        [Fact]
        public void DeclareReference_WithATargetInTheFieldsPosition_Throws()
        {
            var declare = () => Builder().DeclareReference<Party>("id", "probe.widget", "probe.widget#slots");

            declare.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("field");
        }

        [Fact]
        public void Build_ReferenceDeclarations_AreASnapshotALaterDeclarationCannotChange()
        {
            var builder = Builder().DeclareReference<Party>("id", "probe.thing");
            var options = builder.Build();

            builder.DeclareReferenceWhen<Party>("externalId", "externalKind", "thing", null, OxQLKeyAs.Guid, "probe.thing");

            options.ReferenceDeclarations.All.Should().ContainSingle();
            builder.Build().ReferenceDeclarations.All.Should().HaveCount(2);
        }

        [Fact]
        public void Build_WithoutDeclarations_IsTheDocumentOfNoDeclarations()
        {
            SchemaBuild.Build(Builder().Build()).Revision.Should().Be(SchemaBuild.Degraded.Revision);
        }

        [Theory]
        [InlineData("", "probe.thing")]
        [InlineData("id", " ")]
        public void DeclareReference_WithABlankMemberOrTarget_Throws(string wireMember, string target)
        {
            var declare = () => Builder().DeclareReference<Party>(wireMember, target);

            declare.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DeclareReferenceWhen_WithoutATarget_Throws()
        {
            var declare = () => Builder().DeclareReferenceWhen<Party>("externalId", "externalKind", "thing", null);

            declare.Should().Throw<ArgumentException>();
        }
    }
}

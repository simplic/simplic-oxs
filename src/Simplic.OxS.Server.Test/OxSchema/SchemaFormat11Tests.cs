using System.Text.Json;
using MongoDB.Bson;
using OxQL.Core.Binding;
using OxQL.Core.Cursor;
using OxQL.Core.Engine;
using OxQL.Core.Models;
using OxQL.Model;
using OxQL.Model.Addon;
using OxQL.Model.Attributes;
using OxQL.Mongo;
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
                "{\"when\":{\"path\":\"type\",\"equals\":[\"thing\"]},\"targets\":[{\"entity\":\"probe.thing\",\"field\":\"id\"}]}],\"relation\":{\"name\":\"id\"}}");
            RawProperty(registry, "probe.ledger", "referenceId").Should().Be(
                "{\"name\":\"referenceId\",\"kind\":\"string\",\"nullable\":true,\"referenceCases\":[" +
                "{\"when\":{\"path\":\"dataType\",\"equals\":[\"thing\"]},\"keyAs\":\"guid\",\"targets\":[{\"entity\":\"probe.thing\",\"field\":\"id\"}]}],\"relation\":{\"name\":\"reference\"}}");
        }

        [Fact]
        public void Build_EveryReference_CarriesTheRelationNameTheEngineDerived_OnTheMemberAReaderFindsItAt()
        {
            var registry = SchemaBuild.Degraded;
            var document = registry.Document;

            // A reference member is named by itself, without its suffix: the name lies on the member.
            document.Property("probe.ledger", "slotId").Relation.Should().Be(new OxSchemaRelation { Name = "slot" });
            document.Property("probe.ledger", "referenceId").Relation.Should().Be(new OxSchemaRelation { Name = "reference" });

            // A reference on the key member of an embedded object is named at the slot that holds the object.
            document.Property("probe.ledger", "source").Relation.Should().Be(new OxSchemaRelation { Name = "source", Member = "id" });
            RawProperty(registry, "probe.ledger", "source").Should().EndWith(",\"relation\":{\"name\":\"source\",\"member\":\"id\"}}", "the member is the last of the descriptor");

            // No reference, no name: nothing is declared, so nothing else is written.
            document.Property("probe.ledger", "name").Relation.Should().BeNull();
            document.Property("probe.ledger", "owner").Relation.Should().BeNull("the party's id is no reference");
            document.Property("probe.ledger", "entries").Relation.Should().BeNull("an entry's id is no reference");

            // Every name is the engine's: the document writes what the model says and derives nothing itself.
            foreach (var (id, entry) in document.Types)
                foreach (var property in entry.Properties ?? [])
                {
                    var member = registry.Model.TypePool[id].Members.Single(candidate => candidate.WireName == property.Name);

                    (property.Relation?.Name).Should().Be(member.Relation?.Name, $"{id}#{property.Name}");
                    (property.Relation?.Member).Should().Be(member.Relation?.Member, $"{id}#{property.Name}");

                    if (property.References is not null || property.ReferenceCases is not null)
                        property.Relation.Should().NotBeNull($"{id}#{property.Name} carries a reference, so it has a name");
                }

            // A name costs a service nothing: no finding is written for one.
            registry.Findings.Should().NotContain(finding => finding.Detail.Contains("relation", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Read_ADocumentWithRelationNames_GivesTheEngineTheNamesAsPublished()
        {
            var registry = SchemaBuild.Degraded;
            var read = global::OxQL.Model.Build.DocumentModelBuilder.Build(System.Text.Encoding.UTF8.GetString(registry.Body));

            foreach (var (id, type) in registry.Model.TypePool)
                foreach (var member in type.Members)
                    read.TypePool[id].Members.Single(candidate => candidate.WireName == member.WireName).Relation.Should().Be(member.Relation, $"{id}#{member.WireName}");
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

            document.PropertyNames("t_entry").Should().Equal("id", "note", "kind", "items", "thingId");
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

        /// <summary>
        /// The model's own type member: the engine's model holds the constant each class answers
        /// (<c>MemberDef.ByVariant</c>, OxQL 2.1), and the document publishes it as the engine holds
        /// it, with the distinct values as the member's closed list. This is the seam between the
        /// two packages: the walker writes what the model says and derives nothing itself.
        /// </summary>
        [Fact]
        public void Build_TypeMember_PublishesItsValuePerVariantAndItsValues()
        {
            var registry = SchemaBuild.Degraded;
            var kind = registry.Document.Property("t_entry", "kind");

            kind.Stored.Should().BeFalse();
            kind.ByVariant.Should().Equal(new Dictionary<string, string> { ["Entry"] = "entry", ["GroupEntry"] = "group", ["LineEntry"] = "line" },
                "the concrete base answers under the name baseVariant publishes");
            kind.Values!.Select(value => value.Value).Should().Equal("entry", "group", "line");
            kind.Values.Should().OnlyContain(value => value.Label == null);
            RawProperty(registry, "t_entry", "kind").Should().Be(
                "{\"name\":\"kind\",\"kind\":\"string\",\"nullable\":false,\"values\":[{\"value\":\"entry\"},{\"value\":\"group\"},{\"value\":\"line\"}],"
                + "\"stored\":false,\"byVariant\":{\"Entry\":\"entry\",\"GroupEntry\":\"group\",\"LineEntry\":\"line\"}}");

            // A variant's own entry lists the member as what it is there: returned, not stored.
            RawProperty(registry, "t_lineEntry", "kind").Should().Be("{\"name\":\"kind\",\"kind\":\"string\",\"nullable\":false,\"stored\":false}");

            // An unstored member that is no constant of a class has neither.
            registry.Document.Property("probe.widget", "label").ByVariant.Should().BeNull();
            registry.Document.Property("probe.widget", "label").Values.Should().BeNull();
        }

        /// <summary>Explain never executes, so a runner that is called is a failure.</summary>
        private sealed class NoRunner : IAggregateRunner
        {
            public Task<IReadOnlyList<BsonDocument>> AggregateAsync(EntityDef entity, IReadOnlyList<BsonDocument> stages, AggregateRunOptions options, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("Explain must not execute.");
        }

        /// <summary>
        /// What the document says a query can do with the type member is what the query engine does
        /// with it. The document says it in two lists a reader honours without knowing the member
        /// (<c>notFilterable</c>, <c>notSortable</c>); the engine says it in the flags of its explain
        /// answer for the same model. The type member is filtered with <c>eq</c>, <c>neq</c>,
        /// <c>in</c> and <c>nin</c> and is neither sorted nor grouped by; a member that is merely not
        /// stored takes no condition at all. A reader of either source arrives at the same answer.
        /// </summary>
        [Fact]
        public async Task Build_TypeMember_SaysOfFilteringAndSortingWhatTheQueryEnginesExplainSays()
        {
            var registry = SchemaBuild.Degraded;
            var entry = registry.Document.Entry("probe.ledger");

            // The document: the member is not on the list of what cannot be filtered, and is on the list of what cannot be sorted.
            entry.NotFilterable.Should().NotContain(["lead.kind", "entries.kind"], "the query engine filters the type member");
            entry.NotSortable.Should().Equal(["entries.kind", "lead.kind"], "the query engine refuses to sort and to group by it, which nothing else in the descriptors says of a scalar outside a collection");
            entry.NotFilterable.Should().Contain("caption", "a member that is merely not stored stays on the list");
            registry.Document.Entry("probe.widget").NotFilterable.Should().Contain("label");
            registry.Document.Entry("probe.widget").NotSortable.Should().BeEmpty("an entity without a type member lists nothing");

            // The engine, for the same model: the flags of each member in an explain answer with the types written out.
            var options = new OxQLOptions { Cursor = { SigningKey = "test-signing-key" } };
            var engine = new MongoQueryEngine(new StaticEntityModelProvider(registry.Model), new NoRunner(), new CursorCodec("test-signing-key"), options);
            var context = new RequestContext
            {
                Organisation = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Options = options,
                AddonSource = EmptyAddonDefinitionSource.Instance,
                Contract = 2,
            };

            async Task<Dictionary<string, System.Text.Json.Nodes.JsonObject>> FlagsOf(string entity)
            {
                var query = JsonSerializer.Deserialize<QueryRequest>($$"""{ "entityType": "{{entity}}", "pipeline": [] }""", OxQLJson.Wire)!;
                var outcome = await engine.ExplainAsync(new ExplainRequest { Query = query, Include = [ExplainRequest.IncludeTypes], IsEnvelope = true }, context, CancellationToken.None);
                var answer = outcome.Should().BeOfType<ExplainOutcome.Success>(outcome is ExplainOutcome.Refused refused ? string.Join("; ", (refused.Refusal.Errors ?? []).Select(error => error.Code + " " + error.Message)) : "").Subject.Result;
                var table = answer.Types.Single(type => type.Key.EndsWith(entity, StringComparison.Ordinal)).Value!.AsObject();

                return table["members"]!.AsArray().ToDictionary(
                    member => member!.AsArray()[0]!.GetValue<string>(),
                    member => answer.FlagSets![member!.AsArray()[3]!.GetValue<string>()]!.AsObject());
            }

            var ledger = await FlagsOf("probe.ledger");

            foreach (var path in new[] { "lead.kind", "entries.kind" })
            {
                ledger[path]["operators"]!.AsArray().Select(op => op!.GetValue<string>()).Should().Equal(["eq", "neq", "in", "nin"], $"the engine compares '{path}' by the variants that hold a value");
                ledger[path]["sortable"]!.GetValue<bool>().Should().BeFalse(path);
                ledger[path]["groupable"]!.GetValue<bool>().Should().BeFalse(path);
            }

            // A member that is merely not stored: the engine does nothing with it, and the document lists it.
            ledger["caption"]["operators"]!.AsArray().Should().BeEmpty();
            ledger["caption"]["projectable"]!.GetValue<bool>().Should().BeFalse();

            // Every path a list names is one the engine refuses for that use, and no path the engine filters is on the list.
            entry.NotFilterable!.Should().OnlyContain(path => ledger.ContainsKey(path), "the engine's answer lists every member the document does");

            foreach (var path in entry.NotFilterable!)
                ledger[path]["operators"]!.AsArray().Should().BeEmpty($"'{path}' is published as not filterable");

            foreach (var path in entry.NotSortable!)
                ledger[path]["sortable"]!.GetValue<bool>().Should().BeFalse($"'{path}' is published as not sortable");

            ledger.Where(member => member.Value["operators"]!.AsArray().Count > 0).Select(member => member.Key)
                .Should().NotIntersectWith(entry.NotFilterable!, "what the engine filters is not published as not filterable");

            // And a stored scalar of the row is sorted by both: the list holds the exception only.
            ledger["name"]["sortable"]!.GetValue<bool>().Should().BeTrue();
            entry.NotSortable.Should().NotContain("name");
        }

        [Fact]
        public void Build_UnstoredMember_SaysSo_AndAStoredOneSaysNothing()
        {
            var registry = SchemaBuild.Degraded;

            // `label` is in the wire view and has no storage: no row of a query carries it.
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

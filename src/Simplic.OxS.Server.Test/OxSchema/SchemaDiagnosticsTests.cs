using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Test.OxSchema.Fixtures;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>What the build reports, and which half of it reaches the document.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaDiagnosticsTests
    {
        [Fact]
        public void Build_DuplicateEntityId_IsFatalAndPublished()
        {
            var finding = SchemaBuild.Degraded.Findings.Single(
                candidate => candidate.Code == OxSchemaCodes.DuplicateEntityId);

            finding.Target.Should().Be("probe.twin");
            finding.Refuses.Should().BeTrue();
            finding.Published.Should().BeTrue();
            finding.Detail.Should().Be("2 declarations claim this id, so none of them is described.");
        }

        [Fact]
        public void Build_DuplicateEntityId_NamesEveryClaimantInTheLogOnlyHalf()
        {
            var finding = SchemaBuild.Degraded.Findings.Single(
                candidate => candidate.Code == OxSchemaCodes.DuplicateEntityId);

            finding.ClrDetail.Should().Be($"{typeof(TwinA).FullName}, {typeof(TwinB).FullName}");
        }

        [Fact]
        public void Build_OffGrammarEntityId_IsNeitherFatalNorPublished()
        {
            var finding = SchemaBuild.Degraded.Findings.Single(
                candidate => candidate.Code == OxSchemaCodes.EntityIdOffGrammar);

            finding.Target.Should().Be("badid");
            finding.Refuses.Should().BeFalse();
            finding.Published.Should().BeFalse();
        }

        [Fact]
        public void Build_OffGrammarEntityId_KeepsItsFullyDescribedPoolEntry()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Entry("badid").Entity.Should().BeTrue();
            document.PropertyNames("badid").Should().Equal("id", "isDeleted", "name");
        }

        [Fact]
        public void Build_Findings_AreOrderedByCostThenCodeThenTarget()
        {
            var findings = SchemaBuild.Degraded.Findings;

            findings.Select(finding => finding.Code)
                .Should().Equal(
                    OxSchemaCodes.DuplicateEntityId,
                    OxSchemaCodes.CollectionUntyped,
                    OxSchemaCodes.ControllerLinkAmbiguous,
                    OxSchemaCodes.EntityIdOffGrammar,
                    OxSchemaCodes.EntityTypeShared,
                    OxSchemaCodes.ReferenceDeclarationUnresolved,
                    OxSchemaCodes.ReferenceDeclarationUnresolved);
        }

        [Fact]
        public void Build_Diagnostics_CarryOnlyThePublishedFindings()
        {
            var document = SchemaBuild.Degraded.Document;

            document.Diagnostics.Should().NotBeNull();
            document.Diagnostics!.Select(diagnostic => diagnostic.Code)
                .Should().Equal(OxSchemaCodes.DuplicateEntityId);
            document.Diagnostics.Single().Target.Should().Be("probe.twin");
        }

        [Fact]
        public void Build_Diagnostics_NameNoClrType()
        {
            var document = SchemaBuild.Degraded.Document;

            foreach (var diagnostic in document.Diagnostics ?? [])
            {
                diagnostic.Detail.Should().NotContain("Twin");
                diagnostic.Target.Should().NotContain(".Fixtures.");
            }
        }

        [Fact]
        public void Build_WithoutAssemblies_DescribesNothingAndSaysSo()
        {
            var registry = SchemaBuild.Build(SchemaBuild.OptionsWithoutAssemblies());

            registry.Document.Types.Should().BeEmpty();
            registry.Document.Diagnostics!.Select(diagnostic => diagnostic.Code)
                .Should().Equal(OxSchemaCodes.EntityAssembliesMissing);
            registry.Document.Diagnostics.Single().Target.Should().Be(SchemaBuild.Service);
            registry.Body.Should().NotBeEmpty();
        }

        [Fact]
        public void Build_WithoutAssemblies_DegradesInAStrictEnvironment()
        {
            var registry = SchemaBuild.Build(SchemaBuild.OptionsWithoutAssemblies("Local"));

            registry.Document.Types.Should().BeEmpty();
            registry.Document.Diagnostics!.Single().Code.Should().Be(OxSchemaCodes.EntityAssembliesMissing);
        }

        [Fact]
        public void Build_WhenTheEntityScanThrows_DescribesNothingAndSaysSo()
        {
            var registry = SchemaBuild.Build(SchemaBuild.OptionsWithUnloadableAssembly());

            registry.Document.Types.Should().BeEmpty();
            registry.Document.Diagnostics!.Select(diagnostic => diagnostic.Code)
                .Should().Equal(OxSchemaCodes.EntityScanFailed);
            registry.Document.Diagnostics.Single().Target.Should().Be(SchemaBuild.Service);
            registry.Document.Diagnostics.Single().Detail
                .Should().Be("The entity scan failed, so this document describes no types at all.");
        }

        [Fact]
        public void Build_WhenTheEntityScanThrows_KeepsTheLoaderDetailOffTheWire()
        {
            var registry = SchemaBuild.Build(SchemaBuild.OptionsWithUnloadableAssembly());

            var finding = registry.Findings.Single();

            finding.Code.Should().Be(OxSchemaCodes.EntityScanFailed);
            finding.ClrDetail.Should().Contain(nameof(ReflectionTypeLoadException));
            finding.ClrDetail.Should().Contain(UnloadableAssembly.MissingDependency);
            finding.Detail.Should().NotContain(UnloadableAssembly.MissingDependency);
        }

        [Fact]
        public void Build_WhenTheEntityScanThrows_DegradesInAStrictEnvironment()
        {
            var registry = SchemaBuild.Build(SchemaBuild.OptionsWithUnloadableAssembly("Local"));

            registry.Document.Types.Should().BeEmpty();
            registry.Document.Diagnostics!.Single().Code.Should().Be(OxSchemaCodes.EntityScanFailed);
        }

        [Fact]
        public void Build_WhenTheEntityScanThrows_StillProducesTheLegacyDocument()
        {
            var registry = SchemaBuild.Build(SchemaBuild.OptionsWithUnloadableAssembly());

            registry.ModelDefinition.Should().NotBeNull();
            registry.ModelDefinition!.DefinitionCount.Should().Be(4);
            registry.ModelDefinition.Body.Should().NotBeEmpty();
        }

        [Fact]
        public void Build_CleanPool_PublishesNoDanglingPointerOrOffGrammarName()
        {
            var codes = SchemaBuild.Degraded.Findings.Select(finding => finding.Code);

            codes.Should().NotContain(OxSchemaCodes.DanglingTypePointer);
            codes.Should().NotContain(OxSchemaCodes.StructuralIdOffGrammar);
            codes.Should().NotContain(OxSchemaCodes.PropertyNameOffGrammar);
        }

        [Fact]
        public void Build_UntypedCollection_IsDescribedAsUnknownAndReported()
        {
            var registry = SchemaBuild.Degraded;

            registry.Document.Property("probe.bag", "untyped").Of!.Kind.Should().Be(OxSchemaKinds.Unknown);
            registry.Findings.Should().ContainSingle(finding => finding.Code == OxSchemaCodes.CollectionUntyped && finding.Target == "probe.bag#untyped");
            registry.Document.Diagnostics!.Should().NotContain(diagnostic => diagnostic.Code == OxSchemaCodes.CollectionUntyped);
        }

        [Fact]
        public void Build_TwoIdsOnOneType_DescribesTheFirstAndReportsTheSecond()
        {
            var registry = SchemaBuild.Degraded;

            registry.Document.Types.Should().ContainKey("probe.base");
            registry.Document.Types.Should().NotContainKey("probe.leaf");
            registry.Findings.Should().ContainSingle(finding => finding.Code == OxSchemaCodes.EntityTypeShared && finding.Target == "probe.leaf");
        }
    }
}

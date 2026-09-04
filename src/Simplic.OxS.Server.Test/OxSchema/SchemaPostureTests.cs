using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Test.OxSchema.Fixtures;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>Which hosts refuse an ambiguous document and which serve a degraded one.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaPostureTests
    {
        [Theory]
        [InlineData("Development")]
        [InlineData("Local")]
        [InlineData("local")]
        public void Build_StrictEnvironment_RefusesAnAmbiguousDocument(string environmentName)
        {
            var build = () => SchemaBuild.Build(environmentName);

            build.Should().Throw<InvalidOperationException>();
        }

        [Theory]
        [InlineData("Localhost")]
        [InlineData("SchemaLab")]
        [InlineData("Staging")]
        [InlineData("Production")]
        [InlineData("Test")]
        [InlineData("")]
        public void Build_UnnamedEnvironment_ServesADegradedDocument(string environmentName)
        {
            var registry = SchemaBuild.Build(environmentName);

            registry.Document.Types.Should().NotBeEmpty();
            registry.Document.Diagnostics.Should().HaveCount(1);
        }

        [Fact]
        public void Build_ContinuousIntegration_RefusesUnderAnyEnvironmentName()
        {
            var build = () => OxSchemaRegistry.Build(SchemaBuild.OptionsUnderCi("1", "Production"));

            build.Should().Throw<InvalidOperationException>();
        }

        [Theory]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("False")]
        [InlineData(" ")]
        public void Build_ContinuousIntegrationTurnedOff_LeavesThePostureLenient(string ci)
        {
            var registry = OxSchemaRegistry.Build(SchemaBuild.OptionsUnderCi(ci, "Production"));

            registry.Document.Types.Should().NotBeEmpty();
        }

        [Fact]
        public void Build_Refusal_ReportsEveryFatalFindingWithItsClrDetail()
        {
            var build = () => SchemaBuild.Build("Development");

            var message = build.Should().Throw<InvalidOperationException>().Which.Message;

            message.Should().StartWith(
                "Ox schema: refusing to serve 'probe' - 1 ambiguous validation finding.");
            message.Should().Contain($"{OxSchemaCodes.DuplicateEntityId}  probe.twin");
            message.Should().Contain($"[{typeof(TwinA).FullName}, {typeof(TwinB).FullName}]");
        }

        [Fact]
        public void Build_Refusal_NamesNoNonFatalFinding()
        {
            var build = () => SchemaBuild.Build("Development");

            var message = build.Should().Throw<InvalidOperationException>().Which.Message;

            message.Should().NotContain(OxSchemaCodes.EntityIdOffGrammar);
        }

        [Fact]
        public void Build_DegradedDocument_CarriesTheSameContentInEveryLenientEnvironment()
        {
            var first = SchemaBuild.Build("Production");
            var second = SchemaBuild.Build("SchemaLab");

            second.Revision.Should().Be(first.Revision);
        }
    }
}

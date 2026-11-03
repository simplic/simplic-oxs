using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>What a host declares through the options builder, and what the build does with it.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaOptionsTests
    {
        private static OxSchemaOptionsBuilder Builder() => new()
        {
            ServiceName = SchemaBuild.Service,
            ApiName = "probe-api",
            ApiVersion = "v1",
            TypeAssemblies = SchemaBuild.Options().TypeAssemblies,
            ControllerTypes = SchemaBuild.Options().ControllerTypes,
        };

        [Fact]
        public void RetireEntityId_PublishesTheRetiredIdsFirstAndOrdinallySorted()
        {
            var options = Builder().RetireEntityId("probe.widget", "widget", "Old.Widget", " gadget ").Build();

            var aliases = SchemaBuild.Build(options).Document.Entry("probe.widget").Aliases;

            aliases.Should().StartWith(["gadget", "old.widget", "widget"]);
        }

        [Fact]
        public void RetireEntityId_NormalisesTheCurrentIdLikeEveryEntityId()
        {
            var options = Builder().RetireEntityId(" Probe.Widget ", "widget").Build();

            SchemaBuild.Build(options).Document.Entry("probe.widget").Aliases.Should().Contain("widget");
        }

        [Fact]
        public void RetireEntityId_ForAnUnknownEntity_ChangesNothing()
        {
            var options = Builder().RetireEntityId("probe.nothing", "nothing").Build();

            SchemaBuild.Build(options).Revision.Should().Be(SchemaBuild.Degraded.Revision);
        }

        [Fact]
        public void RetireEntityId_WithABlankRetiredId_Throws()
        {
            var retire = () => Builder().RetireEntityId("probe.widget", "widget", " ");

            retire.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("", "probe-api", "v1")]
        [InlineData("probe", " ", "v1")]
        [InlineData("probe", "probe-api", "")]
        public void Build_WithABlankIdentity_Throws(string service, string apiName, string apiVersion)
        {
            var builder = Builder();
            builder.ServiceName = service;
            builder.ApiName = apiName;
            builder.ApiVersion = apiVersion;

            var build = () => builder.Build();

            build.Should().Throw<InvalidOperationException>();
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData(" ", false)]
        [InlineData("0", false)]
        [InlineData("false", false)]
        [InlineData("False", false)]
        [InlineData("1", true)]
        [InlineData("true", true)]
        public void ReadContinuousIntegration_ReadsTheConventionalVariable(string? variable, bool expected)
        {
            OxSchemaBuildOptions.ReadContinuousIntegration(variable).Should().Be(expected);
        }
    }
}

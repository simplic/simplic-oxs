using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The legacy document the registry builds, and the endpoint that serves it.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class ModelDefinitionDocumentTests
    {
        [Fact]
        public void Build_WithDeclaredControllers_ProducesOneDefinitionPerController()
        {
            var document = SchemaBuild.Degraded.ModelDefinition;

            document.Should().NotBeNull();
            document!.DefinitionCount.Should().Be(4);
            document.Models.Should().HaveCount(4);
            document.Failures.Should().BeEmpty();
            document.Body.Should().NotBeEmpty();
        }

        [Fact]
        public void Build_WithNoControllers_ProducesNoDocument()
        {
            var options = SchemaBuild.Options() with { ControllerTypes = [] };

            SchemaBuild.Build(options).ModelDefinition.Should().BeNull();
        }

        [Fact]
        public void Build_Document_IsIndentedJsonWithPinnedCrlfNewlines()
        {
            var text = Encoding.UTF8.GetString(SchemaBuild.Degraded.ModelDefinition!.Body);

            var crlf = text.Split("\r\n").Length - 1;
            var bareLf = text.Replace("\r\n", "", StringComparison.Ordinal).Split('\n').Length - 1;

            crlf.Should().BeGreaterThan(0);
            bareLf.Should().Be(0);
        }

        [Fact]
        public void Build_Document_IsAJsonArrayOfDefinitions()
        {
            using var body = JsonDocument.Parse(SchemaBuild.Degraded.ModelDefinition!.Body);

            body.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
            body.RootElement.GetArrayLength().Should().Be(4);
        }

        [Fact]
        public void Get_ReturnsTheDocumentTheRegistryHolds()
        {
            var registry = SchemaBuild.Degraded;

            var answer = new ModelDefinitionController(registry).Get(CancellationToken.None);

            var file = answer.Should().BeOfType<FileContentResult>().Subject;

            file.ContentType.Should().Be("application/json");
            file.FileContents.Should().Equal(registry.ModelDefinition!.Body);
        }

        [Fact]
        public void Get_WithNoDocument_ReturnsNotFound()
        {
            var options = SchemaBuild.Options() with { ControllerTypes = [] };

            var answer = new ModelDefinitionController(SchemaBuild.Build(options)).Get(CancellationToken.None);

            answer.Should().BeOfType<NotFoundResult>();
        }
    }
}

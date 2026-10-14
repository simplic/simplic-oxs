using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Test.OxSchema;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Simplic.OxS.Server.Test.Extensions
{
    /// <summary>
    /// What the OpenAPI setup takes from the package's own comment file: the members of the
    /// schema document's models and the actions of the schema routes, nothing else of the
    /// assembly, and every reference of a comment in the terms of the wire.
    /// </summary>
    public sealed class PackageCommentsTests : IDisposable
    {
        private const string Namespace = "Simplic.OxS.Server.OxSchema.";

        private readonly string directory = Directory.CreateTempSubdirectory("oxs-package-comments-").FullName;

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(directory, recursive: true);

        /// <summary>The comment file of the assembly under test, as its build wrote it beside this test assembly.</summary>
        private static XDocument Built() => XDocument.Load(Path.Combine(AppContext.BaseDirectory, PackageComments.FileName), LoadOptions.PreserveWhitespace);

        private static List<XElement> Members(XPathDocument published) =>
            [.. XDocument.Parse(published.CreateNavigator().OuterXml).Descendants("member")];

        [Fact]
        public void TheDescribedModels_AreTheOnesTheSchemaRoutesAnswerWith()
        {
            PackageComments.Models.Select(model => model.Name).Should().BeEquivalentTo(
                "OxSchemaApi", "OxSchemaConstraints", "OxSchemaDeprecation", "OxSchemaDiagnostic", "OxSchemaDiscriminator", "OxSchemaDocument", "OxSchemaEntityItem",
                "OxSchemaEnumValue", "OxSchemaLimits", "OxSchemaOperation", "OxSchemaProperty", "OxSchemaReference", "OxSchemaReferenceCase", "OxSchemaReferenceCondition",
                "OxSchemaReferenceTarget", "OxSchemaRelation", "OxSchemaType", "OxSchemaValue", "OxSchemaVariant");
        }

        [Fact]
        public void OfTheBuiltCommentFile_OnlyTheModelsTheirMembersAndTheActionsOfTheSchemaRoutesAreTaken()
        {
            var built = Built();
            var taken = Members(PackageComments.Published(built)).Select(member => (string)member.Attribute("name")!).ToList();

            // The file holds the comments of the whole assembly; a fraction is taken.
            built.Descendants("member").Count().Should().BeGreaterThan(taken.Count * 5);

            var models = PackageComments.Models.Select(model => model.FullName!).ToHashSet();

            taken.Where(id => id.StartsWith("T:", StringComparison.Ordinal)).Select(id => id[2..]).Should().BeEquivalentTo(models);
            taken.Where(id => id.StartsWith("P:", StringComparison.Ordinal)).Select(id => id.Substring(2, id.LastIndexOf('.') - 2)).Should().OnlyContain(declaring => models.Contains(declaring));
            taken.Where(id => id.StartsWith("P:", StringComparison.Ordinal)).Should().HaveCount(102);
            taken.Where(id => id.StartsWith("M:", StringComparison.Ordinal)).Should().BeEquivalentTo(
                $"M:{typeof(SchemaController).FullName}.Get(System.Threading.CancellationToken)",
                $"M:{typeof(SchemaController).FullName}.GetAddonsAsync(System.Threading.CancellationToken)");
            taken.Should().OnlyContain(id => id.StartsWith("T:", StringComparison.Ordinal) || id.StartsWith("P:", StringComparison.Ordinal) || id.StartsWith("M:", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("T:Simplic.OxS.Server.OxSchema.OxSchemaProperty", true)]
        [InlineData("P:Simplic.OxS.Server.OxSchema.OxSchemaProperty.Kind", true)]
        [InlineData("P:Simplic.OxS.Server.OxSchema.OxSchemaReferenceCondition.EqualsAny", true)]
        [InlineData("M:Simplic.OxS.Server.Controller.SchemaController.Get(System.Threading.CancellationToken)", true)]
        // A controller as a whole, what is nested in it, and its constructors' types of other parts.
        [InlineData("T:Simplic.OxS.Server.Controller.SchemaController", false)]
        [InlineData("T:Simplic.OxS.Server.Controller.SchemaController.CodedJson", false)]
        [InlineData("M:Simplic.OxS.Server.Controller.SchemaController.CodedJson.ExecuteResultAsync(Microsoft.AspNetCore.Mvc.ActionContext)", false)]
        [InlineData("M:Simplic.OxS.Server.Controller.SchemaController.#ctor(Simplic.OxS.Server.OxSchema.OxSchemaRegistry,Simplic.OxS.IRequestContext)", false)]
        [InlineData("M:Simplic.OxS.Server.Controller.SchemaController.AddonDefinitions", false)]
        // What the schema is built with, and its lists of constants: no model of a route.
        [InlineData("T:Simplic.OxS.Server.OxSchema.OxSchemaKinds", false)]
        [InlineData("F:Simplic.OxS.Server.OxSchema.OxSchemaKinds.String", false)]
        [InlineData("F:Simplic.OxS.Server.OxSchema.OxSchemaDocument.CurrentSchemaVersion", false)]
        [InlineData("T:Simplic.OxS.Server.OxSchema.OxSchemaBuildOptions", false)]
        [InlineData("P:Simplic.OxS.Server.OxSchema.OxSchemaBuildOptions.RequireAuthorization", false)]
        [InlineData("T:Simplic.OxS.Server.OxSchema.OxSchemaRegistry", false)]
        // The models the addon definition routes describe by attributes, and every other part of the package.
        [InlineData("T:Simplic.OxS.Server.Controllers.Model.CreateAddonDefinitionRequest", false)]
        [InlineData("P:Simplic.OxS.Server.Controllers.Model.CreateAddonDefinitionRequest.Entity", false)]
        [InlineData("M:Simplic.OxS.Server.Controllers.AddonDefinitionController.CreateAsync(Simplic.OxS.Server.Controllers.Model.CreateAddonDefinitionRequest,System.Threading.CancellationToken)", false)]
        [InlineData("T:Simplic.OxS.Server.Controllers.OrganizationSettingsController", false)]
        [InlineData("M:Simplic.OxS.Server.Controllers.ServiceContractController.GetEndpointContracts", false)]
        [InlineData("T:Simplic.OxS.Server.Bootstrap", false)]
        [InlineData("", false)]
        [InlineData("T:", false)]
        [InlineData("Simplic.OxS.Server.OxSchema.OxSchemaProperty", false)]
        public void AMemberOfTheCommentFile_IsTakenOrLeft(string id, bool taken)
        {
            PackageComments.IsPublished(id).Should().Be(taken);
        }

        [Theory]
        // A member of a model: its name on the wire.
        [InlineData("""<see cref="P:Simplic.OxS.Server.OxSchema.OxSchemaProperty.ByVariant" />""", "`byVariant`")]
        [InlineData("""<see cref="P:Simplic.OxS.Server.OxSchema.OxSchemaType.BaseVariant" />""", "`baseVariant`")]
        [InlineData("""<see cref="P:Simplic.OxS.Server.OxSchema.OxSchemaReferenceCondition.EqualsAny" />""", "`equals`")]
        // A model: its name among the document's components.
        [InlineData("""<see cref="T:Simplic.OxS.Server.OxSchema.OxSchemaProperty" />""", "`OxSchemaProperty`")]
        // A class of constants: its values; one constant: its value.
        [InlineData("""<see cref="T:Simplic.OxS.Server.OxSchema.OxSchemaDiscriminatorForms" />""", "`scalar`, `hierarchical`")]
        [InlineData("""<see cref="F:Simplic.OxS.Server.OxSchema.OxSchemaKinds.DateTime" />""", "`dateTime`")]
        [InlineData("""<see cref="F:Simplic.OxS.Server.OxSchema.OxSchemaDocument.CurrentSchemaVersion" />""", "`1.1`")]
        // A keyword and a parameter.
        [InlineData("""<see langword="null" />""", "`null`")]
        [InlineData("""<paramref name="ct" />""", "`ct`")]
        // Anything else of the code: its words, never its name.
        [InlineData("""<see cref="P:Simplic.OxS.Server.OxSchema.OxSchemaBuildOptions.RequireAuthorization" />""", "require authorization")]
        [InlineData("""<see cref="T:Simplic.OxS.Server.OxSchema.OxSchemaRegistry" />""", "ox schema registry")]
        [InlineData("""<see cref="M:Simplic.OxS.Server.OxSchema.OxSchemaPointer.To(System.String)" />""", "to")]
        [InlineData("""<see cref="!:NotResolved" />""", "not resolved")]
        public void AReferenceOfAComment_ReadsInTheTermsOfTheWire(string reference, string read)
        {
            PackageComments.Wire(XElement.Parse(reference)).Should().Be(read);
        }

        [Fact]
        public void WhatIsTaken_HoldsNoReferenceOfTheCode_AndReadsTheSameHoweverItsCommentIsWrapped()
        {
            var comments = XDocument.Parse(
                """
                <doc><members>
                  <member name="P:Simplic.OxS.Server.OxSchema.OxSchemaProperty.Stored">
                    <summary>
                    Only with <see cref="P:Simplic.OxS.Server.OxSchema.OxSchemaProperty.ByVariant" /> <c>false</c>,
                        one of <see cref="T:Simplic.OxS.Server.OxSchema.OxSchemaDiscriminatorForms" />.
                    </summary>
                  </member>
                  <member name="T:Simplic.OxS.Server.Bootstrap"><summary>Left out.</summary></member>
                </members></doc>
                """,
                LoadOptions.PreserveWhitespace);

            var member = Members(PackageComments.Published(comments)).Should().ContainSingle().Subject;

            member.Descendants().Select(element => element.Name.LocalName).Should().Equal("summary", "c");
            member.Element("summary")!.Value.Trim().Should().Be("Only with `byVariant` false, one of `scalar`, `hierarchical`.");

            // The built file: no reference is left in what is taken.
            Members(PackageComments.Published(Built())).SelectMany(taken => taken.Descendants())
                .Should().NotContain(element => element.Name.LocalName == "see" || element.Name.LocalName == "seealso" || element.Name.LocalName == "paramref");
        }

        [Fact]
        public void AFileOfAnotherShape_TakesNothingAndFailsNothing()
        {
            // Well-formed, and nothing of a comment file in it: no member is taken, and the document is generated.
            File.WriteAllText(Path.Combine(directory, PackageComments.FileName), "<settings><members>text</members><member name=\"T:Simplic.OxS.Server.OxSchema.OxSchemaProperty\" /></settings>");

            var options = new SwaggerGenOptions();
            var log = new HardeningCapturedLog();
            var include = () => PackageComments.Include(options, directory, log.CreateLogger("test"));

            include.Should().NotThrow();
            log.Entries.Should().BeEmpty();
        }

        [Fact]
        public void AFileAnotherProcessHolds_IsLeftOutWithAWarning()
        {
            var file = Path.Combine(directory, PackageComments.FileName);

            File.Copy(Path.Combine(AppContext.BaseDirectory, PackageComments.FileName), file);

            var options = new SwaggerGenOptions();
            var log = new HardeningCapturedLog();

            using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var include = () => PackageComments.Include(options, directory, log.CreateLogger("test"));

                include.Should().NotThrow();
            }

            options.SchemaFilterDescriptors.Should().BeEmpty();
            log.Entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
        }

        [Fact]
        public void AHostWithTheFile_IncludesIt_AndAHostWithoutIt_IncludesNothingAndSaysNothing()
        {
            var log = new HardeningCapturedLog();
            var without = new SwaggerGenOptions();

            PackageComments.Include(without, directory, log.CreateLogger("test"));

            without.SchemaFilterDescriptors.Should().BeEmpty();

            File.Copy(Path.Combine(AppContext.BaseDirectory, PackageComments.FileName), Path.Combine(directory, PackageComments.FileName));

            var with = new SwaggerGenOptions();

            PackageComments.Include(with, directory, log.CreateLogger("test"));

            with.SchemaFilterDescriptors.Should().ContainSingle();
            log.Entries.Should().BeEmpty();
        }

        [Theory]
        [InlineData("<doc><members><member name=")]
        [InlineData("not xml at all")]
        [InlineData("")]
        public void AFileThatCannotBeRead_IsLeftOutWithAWarning(string content)
        {
            File.WriteAllText(Path.Combine(directory, PackageComments.FileName), content);

            var options = new SwaggerGenOptions();
            var log = new HardeningCapturedLog();
            var include = () => PackageComments.Include(options, directory, log.CreateLogger("test"));

            include.Should().NotThrow("a comment file must not stand between a service and its OpenAPI document");
            options.SchemaFilterDescriptors.Should().BeEmpty();
            var entry = log.Entries.Should().ContainSingle().Subject;

            entry.Level.Should().Be(LogLevel.Warning);
            entry.Exception.Should().NotBeNull();
            entry.Message.Should().Contain("Simplic.OxS.Server.xml");
        }
    }
}

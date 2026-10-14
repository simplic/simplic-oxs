using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using OxQL.AspNetCore.Controllers;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Controllers;
using Simplic.OxS.Server.Hosting.Test.Fixtures;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// What the OpenAPI document of a service says of the models of the schema document
    /// (<c>OxSchema*</c>): a description of every model and of every member that is no reference
    /// to another model, taken from the package's own comments and said in the terms of the wire;
    /// and that those comments describe nothing else of the package.
    /// </summary>
    public sealed partial class OpenApiSchemaModelTests
    {
        /// <summary>The models of the schema document, as the components of a service's document name them.</summary>
        private static readonly string[] Models =
        [
            "OxSchemaApi", "OxSchemaConstraints", "OxSchemaDeprecation", "OxSchemaDiagnostic", "OxSchemaDiscriminator", "OxSchemaDocument", "OxSchemaEntityItem",
            "OxSchemaEnumValue", "OxSchemaLimits", "OxSchemaOperation", "OxSchemaProperty", "OxSchemaReference", "OxSchemaReferenceCase", "OxSchemaReferenceCondition",
            "OxSchemaReferenceTarget", "OxSchemaRelation", "OxSchemaType", "OxSchemaValue", "OxSchemaVariant",
        ];

        private static string[] Keys(JsonNode? node) => OpenApiDocumentTests.Keys(node);

        private static async Task<JsonNode> SchemasAsync()
        {
            using var host = await BootstrapHost.StartAsync<ParcelStartup>();

            return (await OpenApiDocumentTests.DocumentAsync(host))["components"]!["schemas"]!;
        }

        [Fact]
        public async Task EveryModelOfTheSchemaDocument_AndEveryMemberThatIsNoReferenceToAnotherModel_IsDescribed()
        {
            var schemas = await SchemasAsync();

            Keys(schemas).Where(name => name.StartsWith("OxSchema", StringComparison.Ordinal)).Should().BeEquivalentTo(Models);

            var members = Models.SelectMany(model => schemas[model]!["properties"]!.AsObject().Select(member => (Model: model, Name: member.Key, Schema: member.Value!))).ToList();
            var described = members.Where(member => member.Schema["description"] is not null).ToList();

            Models.Should().OnlyContain(model => schemas[model]!["description"] != null);
            members.Should().HaveCount(102);
            described.Should().HaveCount(92);

            // A member that points to another model has no description of its own beside the pointer: the model's is its.
            members.Except(described).Select(member => $"{member.Model}.{member.Name}").Should().BeEquivalentTo(
                "OxSchemaDocument.api", "OxSchemaDocument.limits", "OxSchemaProperty.of", "OxSchemaProperty.value", "OxSchemaProperty.references", "OxSchemaProperty.constraints",
                "OxSchemaProperty.deprecated", "OxSchemaProperty.relation", "OxSchemaReferenceCase.when", "OxSchemaType.discriminator");
            members.Except(described).Should().OnlyContain(member => member.Schema["$ref"] != null);
        }

        [Fact]
        public async Task TheDescriptions_NameTheMembersOfTheWire_AndNoMemberOfTheCode()
        {
            var schemas = await SchemasAsync();

            string Of(string model, string? member = null) =>
                (member is null ? schemas[model]! : schemas[model]!["properties"]![member]!)["description"]!.GetValue<string>();

            // A member of a model by its wire name, also one that is spelt otherwise in JSON than in the code.
            Of("OxSchemaProperty").Should().Be(
                "A property descriptor. On a type's property list it describes a member; nested as an array's `of` or a dictionary's `value` it describes a shape and carries no member facts.");
            Of("OxSchemaReferenceCondition").Should().Be(
                "The condition of a reference case: either a sibling `path` holding one of `equals`, or the holding object being stored as one of `variant`.");
            Of("OxSchemaReference").Should().Contain("is published as `referenceCases` and never here");
            Of("OxSchemaProperty", "byVariant").Should().Contain("by variant name as `variants` and `baseVariant` spell it").And.Contain("a member with `stored` `false` that");

            // A list of values by the values.
            Of("OxSchemaProperty", "kind").Should().Be(
                "One of `string`, `int`, `long`, `decimal`, `double`, `bool`, `guid`, `date`, `dateTime`, `timeSpan`, `enum`, `binary`, `unknown`, `object`, `array`, `dictionary`.");
            Of("OxSchemaProperty", "storedAs").Should().StartWith("How the value is stored where its kind does not say it, one of `codePoint`, `document`, `arrayOfDocuments`, `arrayOfArrays` (format 1.1).");
            Of("OxSchemaDiscriminator", "form").Should().Be("One of `scalar`, `hierarchical`.");

            // The rule of a relation's name, where the name is.
            Of("OxSchemaRelation", "name").Should().Be(
                "The name of the reference, a wire segment that is unique among the relation names of its type: it lies on the member that carries the reference, "
                + "or, where `member` is present, on the slot one level above, and is then the name of the reference the slot's key member carries.");

            var all = Models.SelectMany(model => schemas[model]!["properties"]!.AsObject()
                    .Select(member => member.Value!["description"]?.GetValue<string>())
                    .Append(schemas[model]!["description"]!.GetValue<string>()))
                .OfType<string>()
                .ToList();

            all.Should().HaveCount(92 + 19);

            foreach (var description in all)
            {
                // One line, whatever the wrapping of the comment it is taken from.
                description.Should().NotMatchRegex(@"[\r\n\t]|  ").And.Be(description.Trim());

                // Nothing of the comment's markup, and outside what is quoted as code no name spelt the way the code spells its members.
                description.Should().NotContainAny("<see", "cref", "Simplic.", "OxQL.");
                CodeName().Matches(Quoted().Replace(description, "")).Select(match => match.Value).Should().BeEmpty(description);
            }
        }

        [Fact]
        public async Task TheCommentsOfThePackage_DescribeNothingElseOfIt()
        {
            using var host = await BootstrapHost.StartAsync<WholePackageStartup>();

            var document = await OpenApiDocumentTests.DocumentAsync(host, "wholepackage-api");
            var schemas = document["components"]!["schemas"]!.AsObject();

            // The routes the package brings beside the query, schema and addon definition routes, and a service's own: no sentence.
            string[] undescribed = ["AddonField", "OrganizationSettings", "ServiceContract", "ServiceDefinition", "Parcel"];

            var operations = document["paths"]!.AsObject()
                .SelectMany(path => path.Value!.AsObject().Select(operation => operation.Value!))
                .Where(operation => undescribed.Contains(operation["tags"]![0]!.GetValue<string>()))
                .ToList();

            operations.Should().HaveCount(19);
            operations.Should().OnlyContain(operation => operation["summary"] == null && operation["description"] == null);
            operations.SelectMany(operation => operation["parameters"]?.AsArray().ToArray() ?? []).Should().OnlyContain(parameter => parameter!["description"] == null);

            // Their models: no description of the model or of a member.
            string[] described = ["OxSchema", "OxQL", "AddonDefinition", "CreateAddonDefinition", "UpdateAddonDefinition"];

            var others = schemas.Where(schema => !described.Any(prefix => schema.Key.StartsWith(prefix, StringComparison.Ordinal))).ToList();

            others.Select(schema => schema.Key).Should().Contain(["OrganizationSettingResult", "ServiceContract", "AddonFieldResponse", "ParcelResponse", "ProblemDetails"]);
            others.Should().OnlyContain(schema => schema.Value!["description"] == null);
            others.SelectMany(schema => schema.Value!["properties"]?.AsObject().Select(member => member.Value) ?? []).Should().OnlyContain(member => member!["description"] == null);

            // The models of the addon definition routes keep the descriptions their members carry as attributes.
            schemas["CreateAddonDefinitionRequest"]!["description"].Should().BeNull();
            schemas["CreateAddonDefinitionRequest"]!["properties"]!["entity"]!["description"]!.GetValue<string>()
                .Should().Be("The id of the extendable entity the property is added to, as the schema document names it (`parcel.parcel`).");

            // The tags stay names: no controller of the package is described as a whole.
            document["tags"]!.AsArray().Should().OnlyContain(tag => Keys(tag).SequenceEqual(new[] { "name" }));
        }

        [GeneratedRegex("`[^`]*`|\"[^\"]*\"")]
        private static partial Regex Quoted();

        /// <summary>Two or more capitalised words in one: <c>ByVariant</c>, <c>OxSchemaKinds</c>.</summary>
        [GeneratedRegex(@"\b(?:[A-Z][a-z0-9]+){2,}\b")]
        private static partial Regex CodeName();
    }

    /// <summary>A service that maps every controller the package brings, beside one of its own.</summary>
    public class WholePackageStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Wholepackage";

        protected override Type[] EntityTypes => [];

        protected override Type[] Controllers =>
        [
            typeof(ParcelController), typeof(OxQLController), typeof(SchemaController), typeof(AddonDefinitionController), typeof(AddonFieldController),
            typeof(OrganizationSettingsController), typeof(ServiceContractController), typeof(ServiceDefinitionController), typeof(ModelDefinitionController),
        ];
    }
}

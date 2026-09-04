using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>The envelope, the canonical serialisation and the revision.</summary>
    [Collection(SchemaCollection.Name)]
    public sealed class SchemaDocumentTests
    {
        private const string RevisionPrefix = "sha256:";

        [Fact]
        public void Build_Envelope_CarriesTheServiceAndItsBasePath()
        {
            var document = SchemaBuild.Degraded.Document;

            document.SchemaVersion.Should().Be("1.0");
            document.Service.Should().Be("probe");
            document.Api.Name.Should().Be("probe-api");
            document.Api.Version.Should().Be("v1");
        }

        [Fact]
        public void Build_ServiceName_IsLowerCased()
        {
            var options = SchemaBuild.Options() with { ServiceName = "Probe" };

            var registry = SchemaBuild.Build(options);

            registry.Document.Service.Should().Be("probe");
        }

        [Fact]
        public void Build_Limits_AreTheQueryEnginesOwnValues()
        {
            var limits = new global::OxQL.Core.Models.OxQLOptions { MaxPageSize = 11, DefaultPageSize = 12, MaxPipelineStages = 13, MaxLookupStages = 14, MaxUnwindStages = 15, MaxGroupFields = 16, MaxProjectionFields = 17, RegexMaxLength = 18 };
            var document = SchemaBuild.Build(SchemaBuild.Options() with { QueryLimits = limits }).Document;
            document.Limits.MaxPageSize.Should().Be(limits.MaxPageSize);
            document.Limits.DefaultPageSize.Should().Be(limits.DefaultPageSize);
            document.Limits.MaxPipelineStages.Should().Be(limits.MaxPipelineStages);
            document.Limits.MaxLookupStages.Should().Be(limits.MaxLookupStages);
            document.Limits.MaxUnwindStages.Should().Be(limits.MaxUnwindStages);
            document.Limits.MaxGroupFields.Should().Be(limits.MaxGroupFields);
            document.Limits.MaxProjectionFields.Should().Be(limits.MaxProjectionFields);
            document.Limits.RegexMaxLength.Should().Be(limits.RegexMaxLength);
        }

        [Fact]
        public void Build_EnvelopeMembers_AreEmittedInADeclaredOrder()
        {
            using var body = JsonDocument.Parse(SchemaBuild.Degraded.Body);

            var members = body.RootElement.EnumerateObject().Select(member => member.Name);

            members.Should().Equal(
                "schemaVersion", "service", "api", "revision", "limits", "diagnostics", "types");
        }

        [Fact]
        public void Build_Revision_IsByteStableAcrossTwoBuilds()
        {
            var first = SchemaBuild.Build();
            var second = SchemaBuild.Build();

            second.Revision.Should().Be(first.Revision);
            second.ETag.Should().Be(first.ETag);
            second.Body.Should().Equal(first.Body);
        }

        [Fact]
        public void Build_Revision_HashesTheDocumentWithoutItself()
        {
            var registry = SchemaBuild.Build();
            var document = registry.Document;
            var published = document.Revision;

            var content = JsonSerializer.SerializeToUtf8Bytes(document with { Revision = null }, OxSchemaJson.Canonical);

            published.Should().Be(RevisionPrefix + Convert.ToHexStringLower(SHA256.HashData(content)));
        }

        [Fact]
        public void Build_ETag_QuotesTheRevisionHash()
        {
            var registry = SchemaBuild.Degraded;

            registry.Revision.Should().StartWith(RevisionPrefix);
            registry.ETag.Should().Be($"\"{registry.Revision[RevisionPrefix.Length..]}\"");
        }

        [Fact]
        public void Build_Revision_IsTheDigestOfTheBodyWithTheMemberCutOut()
        {
            var registry = SchemaBuild.Degraded;
            var text = Encoding.UTF8.GetString(registry.Body);
            var member = $"\"revision\":\"{registry.Revision}\",";

            text.Should().Contain(member);
            text.IndexOf("\"revision\"", StringComparison.Ordinal).Should().Be(text.LastIndexOf("\"revision\"", StringComparison.Ordinal));
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace(member, "")));

            registry.Revision.Should().Be(RevisionPrefix + Convert.ToHexStringLower(digest));
        }

        [Fact]
        public void Build_Body_EscapesEverythingOutsideAscii()
        {
            var text = Encoding.UTF8.GetString(SchemaBuild.Degraded.Body);

            text.Should().Contain("Gr\\u00F6\\u00DFe");
        }

        [Fact]
        public void Build_Body_IsCompactAscii()
        {
            var body = SchemaBuild.Degraded.Body;

            body.Should().OnlyContain(value => value < 0x80);

            var text = Encoding.UTF8.GetString(body);

            text.Should().StartWith("{\"schemaVersion\":\"1.0\",\"service\":\"probe\",\"api\":{\"name\":");
            text.Should().NotContain("\n").And.NotContain("\r").And.NotContain("\t");
        }

        [Fact]
        public void Build_Body_OmitsNullMembers()
        {
            using var body = JsonDocument.Parse(SchemaBuild.Degraded.Body);

            Nulls(body.RootElement).Should().BeEmpty();
        }

        /// <summary>Every member of <paramref name="element"/> whose emitted value is a JSON null.</summary>
        private static IEnumerable<string> Nulls(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var member in element.EnumerateObject())
                    {
                        if (member.Value.ValueKind == JsonValueKind.Null)
                            yield return member.Name;

                        foreach (var nested in Nulls(member.Value))
                            yield return $"{member.Name}.{nested}";
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Null)
                            yield return "[]";

                        foreach (var nested in Nulls(item))
                            yield return nested;
                    }

                    break;
            }
        }

        [Fact]
        public void Build_Body_KeysThePoolInOrdinalOrder()
        {
            using var body = JsonDocument.Parse(SchemaBuild.Degraded.Body);

            var keys = body.RootElement
                .GetProperty("types")
                .EnumerateObject()
                .Select(entry => entry.Name)
                .ToList();

            keys.Should().NotBeEmpty();
            keys.Should().BeInAscendingOrder(StringComparer.Ordinal);
        }

        [Fact]
        public void Build_Body_IsTheSerialisationOfTheDocument()
        {
            var registry = SchemaBuild.Degraded;

            var reserialised = JsonSerializer.SerializeToUtf8Bytes(
                registry.Document, OxSchemaJson.Canonical);

            registry.Body.Should().Equal(reserialised);
        }

        [Fact]
        public void Build_WithoutOptions_Throws()
        {
            var build = () => OxSchemaRegistry.Build(null!);

            build.Should().Throw<ArgumentNullException>();
        }
    }
}

using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>The canonical serialisation of a schema document, and the revision computed over it.</summary>
    public static class OxSchemaJson
    {
        /// <summary>
        /// The canonical form: camelCase members, nulls omitted, no whitespace, non-ASCII escaped.
        /// Every option here is inside the revision hash, the encoder included.
        /// </summary>
        public static readonly JsonSerializerOptions Canonical = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.Default,
            WriteIndented = false,
        };

        /// <summary>Serialises a document canonically.</summary>
        public static byte[] Serialize(OxSchemaDocument document) =>
            JsonSerializer.SerializeToUtf8Bytes(document, Canonical);

        /// <summary><c>sha256:</c> plus the digest of the canonical form with the <c>revision</c> member absent, so a reader can verify it.</summary>
        public static string Revision(OxSchemaDocument document) =>
            "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Serialize(document with { Revision = null })));

        /// <summary>The strong entity tag that carries a revision's digest.</summary>
        public static string EntityTag(string revision) =>
            "\"" + revision["sha256:".Length..] + "\"";
    }
}

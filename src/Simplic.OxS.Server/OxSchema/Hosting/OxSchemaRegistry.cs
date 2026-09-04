namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Everything a host serves about its schema, built once at startup: the document, its
    /// serialised body and revision, the validation findings, and the legacy document beside it.
    /// </summary>
    public sealed class OxSchemaRegistry
    {
        private int logged;

        private OxSchemaRegistry(OxSchemaBuildResult result)
        {
            Document = result.Document;
            Body = result.Body;
            Findings = result.Findings;
            ModelDefinition = result.ModelDefinition;
        }

        /// <summary>Builds the registry from a host's inputs.</summary>
        /// <exception cref="InvalidOperationException">The document is ambiguous and the options fail fast.</exception>
        public static OxSchemaRegistry Build(OxSchemaBuildOptions options) => new(OxSchemaBuilder.Build(options));

        /// <summary>The schema document.</summary>
        public OxSchemaDocument Document { get; }

        /// <summary>The document's revision, <c>sha256:</c> plus the digest of its canonical form.</summary>
        public string Revision => Document.Revision!;

        /// <summary>The strong entity tag the endpoint serves; it carries the revision's digest.</summary>
        public string ETag => OxSchemaJson.EntityTag(Revision);

        /// <summary>The response body of <c>GET /schema</c>, serialised once.</summary>
        public byte[] Body { get; }

        /// <summary>Every validation finding, in the order the log and the diagnostics use.</summary>
        public IReadOnlyList<OxSchemaFinding> Findings { get; }

        /// <summary>The legacy document, or null when the host declares no controllers.</summary>
        public ModelDefinitionDocument? ModelDefinition { get; }

        /// <summary>Claims the one startup log entry for this registry; true for the first caller only.</summary>
        internal bool MarkLogged() => Interlocked.Exchange(ref logged, 1) == 0;
    }
}

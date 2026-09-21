using OxQL.Model;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Everything a host serves about its schema, built once at startup: the entity model the
    /// query engine binds against, the document, its serialised body and revision, the
    /// validation findings, and the legacy document beside it.
    /// </summary>
    public sealed class OxSchemaRegistry
    {
        private int logged;

        private OxSchemaRegistry(OxSchemaBuildResult result, OxSchemaBuildOptions options)
        {
            RequireAuthorization = options.RequireAuthorization;
            Model = result.Model;
            Document = result.Document;
            Body = result.Body;
            Findings = result.Findings;
            ModelDefinition = result.ModelDefinition;
        }

        /// <summary>Builds the registry from a host's inputs.</summary>
        /// <exception cref="InvalidOperationException">The document is ambiguous and the options fail fast.</exception>
        public static OxSchemaRegistry Build(OxSchemaBuildOptions options) => new(OxSchemaBuilder.Build(options), options);

        /// <summary>
        /// Builds the registry of a host whose build threw: a document without types that
        /// carries the <c>entity-scan-failed</c> diagnostic, an empty entity model, and the
        /// legacy document where it can still be generated.
        /// </summary>
        internal static OxSchemaRegistry BuildDegraded(OxSchemaBuildOptions options, Exception cause) =>
            new(OxSchemaBuilder.BuildDegraded(options, cause), options);

        /// <summary>The entity model: the one the document is projected from and the query engine executes against.</summary>
        public EntityModel Model { get; }

        /// <summary>The schema document.</summary>
        public OxSchemaDocument Document { get; }

        /// <summary>The document's revision, <c>sha256:</c> plus the digest of its canonical form.</summary>
        public string Revision => Document.Revision!;

        /// <summary>The strong entity tag the endpoint serves; it carries the revision's digest.</summary>
        public string ETag => OxSchemaJson.EntityTag(Revision);

        /// <summary>The response body of <c>GET /schema</c>, serialised once.</summary>
        public byte[] Body { get; }

        /// <inheritdoc cref="OxSchemaBuildOptions.RequireAuthorization"/>
        public bool RequireAuthorization { get; }

        /// <summary>Every validation finding, in the order the log and the diagnostics use.</summary>
        public IReadOnlyList<OxSchemaFinding> Findings { get; }

        /// <summary>The legacy document, or null when the host declares no controllers.</summary>
        public ModelDefinitionDocument? ModelDefinition { get; }

        /// <summary>Claims the one startup log entry for this registry; true for the first caller only.</summary>
        internal bool MarkLogged() => Interlocked.Exchange(ref logged, 1) == 0;
    }
}

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The schema document a service publishes under <c>GET /schema</c>. Member order is inside
    /// <see cref="Revision"/>, so every record in this folder orders its members explicitly.
    /// </summary>
    public sealed record OxSchemaDocument
    {
        /// <summary>
        /// The format version this package produces. An additive change bumps the minor; a change
        /// a consumer could read wrong bumps the major, which consumers refuse.
        /// </summary>
        public const string CurrentSchemaVersion = "1.0";

        /// <summary>The format version of this document.</summary>
        [JsonPropertyOrder(0)]
        public string SchemaVersion { get; init; } = CurrentSchemaVersion;

        /// <summary>The service name, lower-case.</summary>
        [JsonPropertyOrder(1)]
        public required string Service { get; init; }

        /// <summary>The two segments of the service's API base path.</summary>
        [JsonPropertyOrder(2)]
        public required OxSchemaApi Api { get; init; }

        /// <summary><c>sha256:</c> plus the digest of the canonical form with this member absent. Null only during the build.</summary>
        [JsonPropertyOrder(3)]
        public string? Revision { get; init; }

        /// <summary>The limits the query engine enforces on a request.</summary>
        [JsonPropertyOrder(4)]
        public required OxSchemaLimits Limits { get; init; }

        /// <summary>What the build could not describe. Absent on a clean build, so an incomplete document is distinguishable from an empty service.</summary>
        [JsonPropertyOrder(5)]
        public IReadOnlyList<OxSchemaDiagnostic>? Diagnostics { get; init; }

        /// <summary>The type pool, entities and structural types in one map keyed by id. The ordinal sort is a rule of the canonical form.</summary>
        [JsonPropertyOrder(6)]
        public required ImmutableSortedDictionary<string, OxSchemaType> Types { get; init; }
    }

    /// <summary>The API base path of a service, as the two segments it is composed from: <c>/&lt;name&gt;/&lt;version&gt;</c>.</summary>
    public sealed record OxSchemaApi
    {
        /// <summary>The first segment, e.g. <c>vehicle-api</c>.</summary>
        [JsonPropertyOrder(0)]
        public required string Name { get; init; }

        /// <summary>The second segment verbatim, e.g. <c>v2</c>.</summary>
        [JsonPropertyOrder(1)]
        public required string Version { get; init; }
    }

    /// <summary>The limits a request has to respect before it is submitted, each read from the query engine that enforces it.</summary>
    public sealed record OxSchemaLimits
    {
        /// <summary>The largest page a request may ask for.</summary>
        [JsonPropertyOrder(0)]
        public required int MaxPageSize { get; init; }

        /// <summary>The page size a request that names none gets.</summary>
        [JsonPropertyOrder(1)]
        public required int DefaultPageSize { get; init; }

        /// <summary>The longest pipeline the engine accepts, counting every stage.</summary>
        [JsonPropertyOrder(2)]
        public required int MaxPipelineStages { get; init; }

        /// <summary>How many lookup stages one pipeline may carry.</summary>
        [JsonPropertyOrder(3)]
        public required int MaxLookupStages { get; init; }

        /// <summary>How many unwind stages one pipeline may carry.</summary>
        [JsonPropertyOrder(4)]
        public required int MaxUnwindStages { get; init; }

        /// <summary>How many fields one group stage may carry.</summary>
        [JsonPropertyOrder(5)]
        public required int MaxGroupFields { get; init; }

        /// <summary>How many fields one projection may name.</summary>
        [JsonPropertyOrder(6)]
        public required int MaxProjectionFields { get; init; }

        /// <summary>The longest regex pattern a filter operand may carry, in characters.</summary>
        [JsonPropertyOrder(7)]
        public required int RegexMaxLength { get; init; }
    }

    /// <summary>One thing the build could not describe. Only findings that removed an entity or the whole pool are published; none names a CLR type.</summary>
    public sealed record OxSchemaDiagnostic
    {
        /// <summary>A stable kebab-case code, e.g. <c>duplicate-entity-id</c>.</summary>
        [JsonPropertyOrder(0)]
        public required string Code { get; init; }

        /// <summary>What the finding is about, in wire terms: an entity id, a pool id, or <c>entityId#path</c>.</summary>
        [JsonPropertyOrder(1)]
        public required string Target { get; init; }

        /// <summary>One sentence, including what was dropped as a result.</summary>
        [JsonPropertyOrder(2)]
        public required string Detail { get; init; }
    }
}

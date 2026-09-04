namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The validation finding codes and their two independent costs: whether a fail-fast host
    /// refuses to start, and whether the finding is published in <c>diagnostics</c>.
    /// </summary>
    /// <remarks>
    /// A host refuses only on ambiguity. A finding is published only where absence could not mark
    /// it (a dropped entity, an empty pool); every other finding stays off the wire, because
    /// publishing it would make consumers refuse a document that is complete.
    /// </remarks>
    public static class OxSchemaCodes
    {
        /// <summary>Two declarations claim one entity id; neither is described.</summary>
        public const string DuplicateEntityId = "duplicate-entity-id";

        /// <summary>A pointer names no pool entry.</summary>
        public const string DanglingTypePointer = "dangling-type-pointer";

        /// <summary>The entity scan threw; the pool is empty.</summary>
        public const string EntityScanFailed = "entity-scan-failed";

        /// <summary>The host named no assemblies to scan; the pool is empty.</summary>
        public const string EntityAssembliesMissing = "entity-assemblies-missing";

        /// <summary>An entity id is not <c>&lt;service&gt;.&lt;entity&gt;</c> in lower-case segments.</summary>
        public const string EntityIdOffGrammar = "entity-id-off-grammar";

        /// <summary>A structural id is not <c>t_</c> plus a camelCase segment.</summary>
        public const string StructuralIdOffGrammar = "structural-id-off-grammar";

        /// <summary>A property name is not a camelCase path segment, so no path can name it.</summary>
        public const string PropertyNameOffGrammar = "property-name-off-grammar";

        /// <summary>Two controllers claim one entity by convention; the entity is linked to neither.</summary>
        public const string ControllerLinkAmbiguous = "controller-link-ambiguous";

        /// <summary>A reference declaration could not be resolved; the reference is not emitted.</summary>
        public const string ReferenceDeclarationUnresolved = "reference-declaration-unresolved";

        /// <summary>A collection or dictionary declares no element type; its values are <c>unknown</c>.</summary>
        public const string CollectionUntyped = "collection-untyped";

        /// <summary>Two entity ids resolve to one CLR type; only the first is described as that type.</summary>
        public const string EntityTypeShared = "entity-type-shared";

        private static readonly string[] Refusing = [DuplicateEntityId, DanglingTypePointer];

        private static readonly string[] Published =
        [
            DuplicateEntityId, DanglingTypePointer, EntityScanFailed, EntityAssembliesMissing,
        ];

        /// <summary>Whether a fail-fast host refuses to start on this code.</summary>
        public static bool Refuses(string code) => Refusing.Contains(code, StringComparer.Ordinal);

        /// <summary>Whether this code reaches the document's <c>diagnostics</c>.</summary>
        public static bool IsPublished(string code) => Published.Contains(code, StringComparer.Ordinal);
    }

    /// <summary>One validation finding.</summary>
    /// <param name="Code">One of <see cref="OxSchemaCodes"/>.</param>
    /// <param name="Target">What it is about, in wire terms: an entity id, a pool id, or <c>entityId#path</c>.</param>
    /// <param name="Detail">One sentence, safe to publish; names no CLR type.</param>
    /// <param name="ClrDetail">The CLR names that make the log line actionable. Never serialised.</param>
    public sealed record OxSchemaFinding(string Code, string Target, string Detail, string? ClrDetail = null)
    {
        /// <summary>Whether a fail-fast host refuses to start on this finding.</summary>
        public bool Refuses => OxSchemaCodes.Refuses(Code);

        /// <summary>Whether this finding is published in the document's <c>diagnostics</c>.</summary>
        public bool Published => OxSchemaCodes.IsPublished(Code);

        /// <summary>Refusing first, then published, then log-only; the order the diagnostics and the log use.</summary>
        public int Rank => Refuses ? 0 : Published ? 1 : 2;

        /// <summary>The published form of this finding.</summary>
        public OxSchemaDiagnostic ToDiagnostic() => new() { Code = Code, Target = Target, Detail = Detail };
    }

    /// <summary>Collects findings during a build and hands them back in their published order.</summary>
    internal sealed class FindingCollector
    {
        private readonly List<OxSchemaFinding> findings = [];

        /// <summary>Records a finding.</summary>
        public void Add(OxSchemaFinding finding) => findings.Add(finding);

        /// <summary>Records a finding.</summary>
        public void Add(string code, string target, string detail, string? clrDetail = null) =>
            findings.Add(new OxSchemaFinding(code, target, detail, clrDetail));

        /// <summary>Every finding, ordered by rank, code and target, so the diagnostics and the log are identical across restarts.</summary>
        public IReadOnlyList<OxSchemaFinding> Sorted() =>
        [
            .. findings
                .OrderBy(finding => finding.Rank)
                .ThenBy(finding => finding.Code, StringComparer.Ordinal)
                .ThenBy(finding => finding.Target, StringComparer.Ordinal),
        ];
    }
}

using System.Text.RegularExpressions;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Checks the finished pool: id grammar, property-name grammar and pointer integrity. Over the
    /// document rather than the CLR graph, because the document is the artefact with the contract.
    /// </summary>
    internal static partial class DocumentValidator
    {
        [GeneratedRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$", RegexOptions.CultureInvariant)]
        private static partial Regex EntityId();

        [GeneratedRegex(@"^[a-z][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant)]
        private static partial Regex PathSegment();

        [GeneratedRegex(@"^t_[a-z][a-zA-Z0-9_]*$", RegexOptions.CultureInvariant)]
        private static partial Regex StructuralId();

        /// <summary>Whether an id is <c>&lt;service&gt;.&lt;entity&gt;</c> with lower-case segments.</summary>
        public static bool IsEntityId(string id) => EntityId().IsMatch(id);

        /// <summary>Whether a name is one camelCase path segment.</summary>
        public static bool IsPathSegment(string name) => PathSegment().IsMatch(name);

        /// <summary>Whether an id is <c>t_</c> plus one camelCase segment.</summary>
        public static bool IsStructuralId(string id) => StructuralId().IsMatch(id);

        /// <summary>Records every grammar and integrity finding of the pool.</summary>
        public static void Inspect(IReadOnlyDictionary<string, OxSchemaType> pool, FindingCollector findings)
        {
            foreach (var (id, entry) in pool)
            {
                if (entry.Entity)
                {
                    if (!IsEntityId(id))
                        findings.Add(
                            OxSchemaCodes.EntityIdOffGrammar,
                            id,
                            "Not <service>.<entity> with [a-z][a-z0-9_]* segments. Still resolves; rename the declared id and retire this one.");
                }
                else if (!IsStructuralId(id))
                {
                    findings.Add(
                        OxSchemaCodes.StructuralIdOffGrammar,
                        id,
                        "Not t_ plus a camelCase segment. The generator minted this, so it is a generator fault.");
                }

                foreach (var property in entry.Properties ?? [])
                {
                    var path = $"{id}#{property.Name}";

                    if (property.Name is null || !IsPathSegment(property.Name))
                        findings.Add(
                            OxSchemaCodes.PropertyNameOffGrammar,
                            path,
                            "Not a [a-z][a-zA-Z0-9_]* path segment, so no path can name this member.");

                    foreach (var pointer in DescriptorVisitor.Pointers(property))
                        if (!pool.ContainsKey(OxSchemaPointer.Strip(pointer)))
                            findings.Add(
                                OxSchemaCodes.DanglingTypePointer,
                                $"{path} -> {pointer}",
                                "The pointer has no pool entry, so this member cannot be resolved.");

                    foreach (var source in DescriptorVisitor.SnapshotSources(property))
                        if (!pool.TryGetValue(source, out var target) || !target.Entity)
                            findings.Add(
                                OxSchemaCodes.DanglingTypePointer,
                                $"{path} -> {source}",
                                "The snapshot source is not an entity of this document, so this member cannot be resolved.");
                }
            }
        }
    }
}

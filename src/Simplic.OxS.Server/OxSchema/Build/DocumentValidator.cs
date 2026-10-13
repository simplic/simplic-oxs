using System.Text.RegularExpressions;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Checks the finished pool: id grammar, property-name grammar and pointer integrity (every
    /// property's pointers, snapshot sources and every variant's pointer). Over the
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

        [GeneratedRegex(@"^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
        private static partial Regex IdSegment();

        /// <summary>Whether an id is <c>&lt;service&gt;.&lt;entity&gt;</c> with lower-case segments.</summary>
        public static bool IsEntityId(string id) => EntityId().IsMatch(id);

        /// <summary>The first segment of an entity id: the service that owns the entity.</summary>
        private static string Prefix(string id) => id.Split('.')[0];

        /// <summary><c>&lt;service&gt;.&lt;rest&gt;</c> where that is an entity id; null where the service name or the rest cannot form one.</summary>
        private static string? Prefixed(string? service, string rest) =>
            service is not null && IsEntityId($"{service}.{rest}") ? $"{service}.{rest}" : null;

        /// <summary>
        /// What to do about an id that has to change: the id to declare, where one can be
        /// derived, and the line that keeps the present id resolving. The service's own name is
        /// asked for as the prefix only where it can be one (<paramref name="ownPrefix"/>).
        /// </summary>
        private static string Rename(string id, string? renamed, bool ownPrefix = true) => renamed is null
            ? $"Declare an id <service>.<entity>{(ownPrefix ? " with this service's name as the prefix" : "")} in [OxQLType], and keep this one resolving with schema.RetireEntityId(\"<service>.<entity>\", \"{id}\"); in ConfigureOxSchema."
            : $"Declare the id '{renamed}' in [OxQLType], and keep this one resolving with schema.RetireEntityId(\"{renamed}\", \"{id}\"); in ConfigureOxSchema.";

        /// <summary>Whether a name is one camelCase path segment.</summary>
        public static bool IsPathSegment(string name) => PathSegment().IsMatch(name);

        /// <summary>Whether an id is <c>t_</c> plus one camelCase segment.</summary>
        public static bool IsStructuralId(string id) => StructuralId().IsMatch(id);

        /// <summary>
        /// Records every grammar and integrity finding of the pool.
        /// </summary>
        /// <param name="pool">The finished pool.</param>
        /// <param name="findings">Where the findings go.</param>
        /// <param name="service">
        /// The host's service name in lower case: the prefix every entity id of the host has to
        /// carry, because another service routes a reference by it. Null leaves the prefix
        /// unchecked.
        /// </param>
        public static void Inspect(IReadOnlyDictionary<string, OxSchemaType> pool, FindingCollector findings, string? service = null)
        {
            // A service name that is no id segment can prefix no id. No id of such a service is
            // held to the prefix, which no rename could give it; the service is told once.
            var prefixes = service is null || IdSegment().IsMatch(service);

            if (!prefixes && pool.Values.Any(entry => entry.Entity))
                findings.Add(
                    OxSchemaCodes.ServiceNameNoIdPrefix,
                    service!,
                    $"The service name '{service}' is not one [a-z][a-z0-9_]* segment, so no entity id can carry it as its prefix. "
                    + "Another service routes a reference by the prefix of the id it names: the entities of this service cannot be referenced from another service.");

            foreach (var (id, entry) in pool)
            {
                if (entry.Entity)
                {
                    if (!IsEntityId(id))
                        findings.Add(
                            OxSchemaCodes.EntityIdOffGrammar,
                            id,
                            (prefixes ? "Not <service>.<entity> with [a-z][a-z0-9_]* segments, so no other service can reference this entity. " : "Not <service>.<entity> with [a-z][a-z0-9_]* segments. ")
                            + Rename(id, id.Contains('.', StringComparison.Ordinal) ? null : Prefixed(service, id), prefixes));
                    else if (prefixes && service is not null && Prefix(id) != service)
                        findings.Add(
                            OxSchemaCodes.EntityIdForeignService,
                            id,
                            $"The prefix '{Prefix(id)}' is not this service's name ('{service}'). Another service routes a reference by the prefix, "
                            + $"to InternalHosts:{Prefix(id)}, and does not reach this entity. "
                            + Rename(id, Prefixed(service, id[(Prefix(id).Length + 1)..])));
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

                foreach (var variant in entry.Variants ?? [])
                    if (!pool.ContainsKey(OxSchemaPointer.Strip(variant.Type)))
                        findings.Add(
                            OxSchemaCodes.DanglingTypePointer,
                            $"{id} -> {variant.Type}",
                            $"The variant '{variant.Name}' points at no pool entry, so a value stored as it cannot be resolved.");
            }
        }
    }
}

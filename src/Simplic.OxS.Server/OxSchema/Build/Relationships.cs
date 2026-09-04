using System.Reflection;
using Simplic.OxS.ModelDefinition.Extenstion.Abstractions;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The relationships between entities: an embedded copy of an entity (<c>snapshotOf</c>) and
    /// a foreign key (<c>references</c>). Every target is an entity id of this document; where
    /// none resolves, nothing is emitted.
    /// </summary>
    internal static class Relationships
    {
        /// <summary>Wire-name suffixes that make a guid property a foreign-key candidate.</summary>
        private static readonly string[] IdSuffixes = ["Id", "Guid"];

        /// <summary>
        /// The last id segment, lower-cased, of every entity to its id. A segment two entities
        /// share is claimed by neither.
        /// </summary>
        public static IReadOnlyDictionary<string, string> EntityIndex(IEnumerable<string> entityIds)
        {
            var index = new Dictionary<string, string>(StringComparer.Ordinal);
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);

            foreach (var id in entityIds)
            {
                var segment = id.Split('.')[^1].ToLowerInvariant();

                if (!index.TryAdd(segment, id))
                    ambiguous.Add(segment);
            }

            foreach (var segment in ambiguous)
                index.Remove(segment);

            return index;
        }

        /// <summary>
        /// The wire names of the id properties a type declares a target entity for through
        /// <c>[ReferenceId]</c>, each mapped to that entity. The attribute sits on the navigation
        /// property and names the paired id property; the entity is the navigation property's
        /// own type. A declaration whose type is not an entity, or whose named property does not
        /// exist, yields nothing and is reported.
        /// </summary>
        public static IReadOnlyDictionary<string, string> DeclaredTargets(
            Type owner,
            string ownerKey,
            Func<Type, string?> entityIdOf,
            FindingCollector findings)
        {
            var targets = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var navigation in TypePoolWalker.PublishedProperties(owner))
            {
                if (navigation.GetCustomAttribute<ReferenceIdAttribute>() is not { } declaration)
                    continue;

                var navigationType = Nullable.GetUnderlyingType(navigation.PropertyType) ?? navigation.PropertyType;
                var target = $"{ownerKey}#{EntityMetadata.WireNames.ConvertName(navigation.Name)}";

                if (entityIdOf(navigationType) is not { } entity)
                {
                    findings.Add(
                        OxSchemaCodes.ReferenceDeclarationUnresolved,
                        target,
                        "The navigation property's type is not an entity of this document, so no reference is emitted.",
                        $"{owner.FullName}.{navigation.Name}: {navigationType.FullName}");

                    continue;
                }

                var idProperty = owner.GetProperty(declaration.ReferenceIdPropertyName, BindingFlags.Public | BindingFlags.Instance);

                if (idProperty is null)
                {
                    findings.Add(
                        OxSchemaCodes.ReferenceDeclarationUnresolved,
                        target,
                        $"The declaration names the id property '{declaration.ReferenceIdPropertyName}', which the type does not have.",
                        $"{owner.FullName}.{navigation.Name}");

                    continue;
                }

                targets[EntityMetadata.WireNames.ConvertName(idProperty.Name)] = entity;
            }

            return targets;
        }

        /// <summary>The reference a guid property carries: the declared target first, the naming convention second, or null.</summary>
        public static OxSchemaReference? Of(
            string wireName,
            IReadOnlyDictionary<string, string> declared,
            IReadOnlyDictionary<string, string> entityIndex)
        {
            if (declared.TryGetValue(wireName, out var target))
                return new OxSchemaReference { Entity = target, Joinable = false, Inferred = false };

            return InferredTarget(wireName, entityIndex) is { } inferred
                ? new OxSchemaReference { Entity = inferred, Joinable = false, Inferred = true }
                : null;
        }

        /// <summary>
        /// Fills every reference's <c>field</c> from the target entity's key, over the finished
        /// pool. Absent when the target is not an entity of this document or its key is not a
        /// single path.
        /// </summary>
        public static Dictionary<string, OxSchemaType> ResolveFields(Dictionary<string, OxSchemaType> pool)
        {
            var resolved = new Dictionary<string, OxSchemaType>(pool.Count, StringComparer.Ordinal);

            foreach (var (key, entry) in pool)
                resolved[key] = entry.Properties is null
                    ? entry
                    : entry with
                    {
                        Properties = [.. entry.Properties.Select(property => property.References is { } reference
                            ? property with { References = reference with { Field = KeyField(pool, reference.Entity) } }
                            : property)],
                    };

            return resolved;
        }

        private static string? KeyField(Dictionary<string, OxSchemaType> pool, string entity) =>
            pool.TryGetValue(entity, out var target) && target.Entity && target.Key is [var single] ? single : null;

        /// <summary>
        /// The entity a guid property points at by name: the wire name minus an id suffix must
        /// equal an entity's last id segment exactly.
        /// </summary>
        private static string? InferredTarget(string wireName, IReadOnlyDictionary<string, string> entityIndex)
        {
            foreach (var suffix in IdSuffixes)
            {
                if (wireName.Length <= suffix.Length || !wireName.EndsWith(suffix, StringComparison.Ordinal))
                    continue;

                if (entityIndex.TryGetValue(wireName[..^suffix.Length].ToLowerInvariant(), out var entity))
                    return entity;
            }

            return null;
        }
    }
}

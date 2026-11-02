using Simplic.OxS.ModelDefinition;
using LegacyDefinition = Simplic.OxS.ModelDefinition.ModelDefinition;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The <c>items</c> member of an entity: every path under it whose terminal is an array of a
    /// keyed item type, with the two-part legacy model ids the service's own legacy document
    /// publishes for the same collection.
    /// </summary>
    internal static class ItemCollections
    {
        /// <summary>The item collections of one entity, in walk order; empty when it has none.</summary>
        public static IReadOnlyList<OxSchemaEntityItem> Of(
            IReadOnlyDictionary<string, OxSchemaType> pool,
            string entityId,
            ModelDefinitionDocument? legacy)
        {
            var paths = new List<string>();

            Collect(pool, entityId, prefix: "", [entityId], paths);

            var aliases = paths.ToDictionary(path => path, _ => new List<string>(), StringComparer.Ordinal);

            // The parent half of a legacy id is one of the entity's own $ClassName aliases.
            var parents = (pool[entityId].Aliases ?? []).Where(alias => alias.StartsWith('$'));

            // Sorted, because a legacy id two paths claim resolves to neither and the sort keeps
            // the walk out of the revision hash.
            var claims = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var parent in parents)
            {
                if (Definition(legacy, parent) is not { } definition)
                    continue;

                foreach (var path in paths)
                {
                    if (Alias(definition, path) is not { } alias)
                        continue;

                    if (!claims.TryGetValue(alias, out var claimants))
                        claims[alias] = claimants = [];

                    claimants.Add(path);
                }
            }

            foreach (var (alias, claimants) in claims)
                if (claimants.Count == 1)
                    aliases[claimants[0]].Add(alias);

            return [.. paths.Select(path => new OxSchemaEntityItem { Path = path, Aliases = aliases[path] })];
        }

        /// <summary>
        /// Walks the pool from one type through object properties and item collections. An
        /// entity pointer is a boundary in both directions: a copy of another entity's row is
        /// neither walked into nor an item collection itself.
        /// </summary>
        private static void Collect(
            IReadOnlyDictionary<string, OxSchemaType> pool,
            string typeId,
            string prefix,
            List<string> stack,
            List<string> paths)
        {
            if (!pool.TryGetValue(typeId, out var entry))
                return;

            foreach (var property in entry.Properties ?? [])
            {
                if (property.Name is null)
                    continue;

                var path = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";

                var element = property.Kind == OxSchemaKinds.Array ? property.Of : null;
                var target = element is not null
                    ? element.Kind == OxSchemaKinds.Object ? element.Type : null
                    : property.Kind == OxSchemaKinds.Object ? property.Type : null;

                if (target is null)
                    continue;

                var id = OxSchemaPointer.Strip(target);

                if (!pool.TryGetValue(id, out var pointee) || pointee.Entity)
                    continue;

                if (element is not null && pointee.Key is { Count: > 0 })
                    paths.Add(path);

                if (stack.Contains(id, StringComparer.Ordinal))
                    continue;

                stack.Add(id);
                Collect(pool, id, path, stack, paths);
                stack.RemoveAt(stack.Count - 1);
            }
        }

        /// <summary>The legacy entry a <c>$ClassName</c> names, or null when the document publishes it zero or several times.</summary>
        private static LegacyDefinition? Definition(ModelDefinitionDocument? legacy, string model)
        {
            var matches = (legacy?.Models ?? []).Where(definition => string.Equals(definition.Model, model, StringComparison.Ordinal)).ToList();

            return matches.Count == 1 ? matches[0] : null;
        }

        /// <summary>
        /// The two-part legacy id for one path, or null when the legacy document does not describe
        /// an item collection there. Segments match case-insensitively because the legacy
        /// generator spells acronyms differently from the response serializer.
        /// </summary>
        private static string? Alias(LegacyDefinition definition, string path)
        {
            var segments = path.Split('.');
            var properties = definition.Properties;

            for (var index = 0; index < segments.Length; index++)
            {
                var property = properties?.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, segments[index], StringComparison.OrdinalIgnoreCase));

                if (property is null)
                    return null;

                if (index == segments.Length - 1)
                    return property.ArrayType is { } arrayType && Publishes(definition, arrayType)
                        ? $"{definition.Model}.{arrayType}"
                        : null;

                var hop = property.ArrayType ?? property.Type;

                if (hop is null || !Publishes(definition, hop))
                    return null;

                properties = PropertiesOf(definition, hop);
            }

            return null;
        }

        private static bool Publishes(LegacyDefinition definition, string model) =>
            model.StartsWith('$')
            && (string.Equals(definition.Model, model, StringComparison.Ordinal)
                || definition.References.Any(reference => string.Equals(reference.Model, model, StringComparison.Ordinal)));

        private static IList<PropertyDefinition>? PropertiesOf(LegacyDefinition definition, string model) =>
            string.Equals(definition.Model, model, StringComparison.Ordinal)
                ? definition.Properties
                : definition.References.FirstOrDefault(reference => string.Equals(reference.Model, model, StringComparison.Ordinal))?.Properties;
    }
}

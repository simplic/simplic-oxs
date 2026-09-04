using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// The ids of structural pool entries: <c>t_</c> plus the CLR type name in camelCase, with a
    /// hash tail where two pooled types share a name. Assigned in one pass over the finished
    /// pool, because whether a name is shared is a property of the whole document.
    /// </summary>
    internal static class StructuralIds
    {
        /// <summary>The prefix of every structural id.</summary>
        public const string Prefix = "t_";

        private const int InitialTailLength = 6;

        /// <summary>
        /// Replaces every working key with the type's structural id and rewrites every pointer
        /// accordingly. Entities keep their declared ids. The result is the ordinally sorted pool.
        /// </summary>
        /// <remarks>
        /// Every claimant of a shared name takes a tail, never only the later ones, so the ids
        /// cannot depend on the order the pool was discovered in. A tail is the leading hex
        /// digits of a digest over the type's version-free CLR identity, and it is widened until
        /// it is unique, so no two entries can end up under one key and no working key can
        /// survive into the document.
        /// </remarks>
        public static ImmutableSortedDictionary<string, OxSchemaType> Assign(
            Dictionary<string, OxSchemaType> pool,
            IReadOnlyDictionary<Type, string> keys)
        {
            var claimants = new SortedDictionary<string, List<Type>>(StringComparer.Ordinal);

            foreach (var (clrType, key) in keys)
            {
                if (pool[key].Entity)
                    continue;

                var readable = ReadableId(clrType);

                if (!claimants.TryGetValue(readable, out var types))
                    claimants[readable] = types = [];

                types.Add(clrType);
            }

            var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            var taken = new HashSet<string>(pool.Keys.Where(key => pool[key].Entity), StringComparer.Ordinal);

            foreach (var (readable, types) in claimants)
                foreach (var clrType in types.OrderBy(ClrIdentity, StringComparer.Ordinal))
                {
                    var id = types.Count == 1 ? readable : Tailed(readable, clrType, taken);

                    taken.Add(id);
                    renamed[keys[clrType]] = id;
                }

            string Rename(string key) => renamed.GetValueOrDefault(key, key);

            return ImmutableSortedDictionary.CreateRange(
                StringComparer.Ordinal,
                pool.Select(pair => new KeyValuePair<string, OxSchemaType>(
                    Rename(pair.Key),
                    pair.Value.Properties is null
                        ? pair.Value
                        : pair.Value with
                        {
                            Properties = [.. pair.Value.Properties.Select(property => DescriptorVisitor.Repoint(property, Rename))],
                        })));
        }

        private static string Tailed(string readable, Type clrType, HashSet<string> taken)
        {
            var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ClrIdentity(clrType))));

            for (var length = InitialTailLength; length <= digest.Length; length += 2)
            {
                var candidate = $"{readable}_{digest[..length]}";

                if (!taken.Contains(candidate))
                    return candidate;
            }

            throw new InvalidOperationException($"Two pooled types share the CLR identity '{ClrIdentity(clrType)}'.");
        }

        /// <summary>The bare structural id of a type: its CLR name without a generic arity suffix, camelCased.</summary>
        public static string ReadableId(Type type)
        {
            var name = type.Name;
            var arity = name.IndexOf('`', StringComparison.Ordinal);

            if (arity > 0)
                name = name[..arity];

            return Prefix + EntityMetadata.WireNames.ConvertName(name);
        }

        /// <summary>
        /// A type's identity without any version: nesting chain or namespace, name, generic
        /// arguments with their assembly, and the assembly's simple name. This spelling is inside
        /// every hash tail.
        /// </summary>
        public static string ClrIdentity(Type type)
        {
            var identity = new StringBuilder();

            AppendIdentity(identity, type);
            identity.Append(", ").Append(type.Assembly.GetName().Name);

            return identity.ToString();
        }

        private static void AppendIdentity(StringBuilder identity, Type type)
        {
            if (type.DeclaringType is not null)
            {
                AppendIdentity(identity, type.DeclaringType);
                identity.Append('+');
            }
            else if (!string.IsNullOrEmpty(type.Namespace))
            {
                identity.Append(type.Namespace).Append('.');
            }

            identity.Append(type.Name);

            if (!type.IsConstructedGenericType)
                return;

            identity.Append('[');

            var arguments = type.GetGenericArguments();

            for (var index = 0; index < arguments.Length; index++)
            {
                if (index > 0)
                    identity.Append(',');

                AppendIdentity(identity, arguments[index]);
                identity.Append(", ").Append(arguments[index].Assembly.GetName().Name);
            }

            identity.Append(']');
        }
    }
}

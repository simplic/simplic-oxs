using System.Collections;
using System.Globalization;
using System.Reflection;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Fills the type pool from CLR types: one entry per type reachable from an entity, nothing
    /// inlined. A type's key is registered before its members are walked, which is what makes
    /// cycles safe without a depth limit.
    /// </summary>
    /// <remarks>
    /// Structural entries are registered under working keys that never reach the document;
    /// <see cref="StructuralIds.Assign"/> replaces them once the pool is complete.
    /// </remarks>
    internal sealed class TypePoolWalker
    {
        /// <summary>
        /// The leaf boundary and the scalar vocabulary in one table: a type listed here stops the
        /// walk and is described by its kind. Narrow integers collapse onto <c>int</c>, unsigned
        /// 64-bit onto <c>long</c>; <c>object</c> is <c>unknown</c>; a time of day has no kind.
        /// </summary>
        private static readonly Dictionary<Type, string> ScalarKinds = new()
        {
            [typeof(string)] = OxSchemaKinds.String,
            [typeof(char)] = OxSchemaKinds.String,
            [typeof(Uri)] = OxSchemaKinds.String,
            [typeof(bool)] = OxSchemaKinds.Bool,
            [typeof(sbyte)] = OxSchemaKinds.Int,
            [typeof(byte)] = OxSchemaKinds.Int,
            [typeof(short)] = OxSchemaKinds.Int,
            [typeof(ushort)] = OxSchemaKinds.Int,
            [typeof(int)] = OxSchemaKinds.Int,
            [typeof(uint)] = OxSchemaKinds.Long,
            [typeof(long)] = OxSchemaKinds.Long,
            [typeof(ulong)] = OxSchemaKinds.Long,
            [typeof(float)] = OxSchemaKinds.Double,
            [typeof(double)] = OxSchemaKinds.Double,
            [typeof(decimal)] = OxSchemaKinds.Decimal,
            [typeof(Guid)] = OxSchemaKinds.Guid,
            [typeof(DateOnly)] = OxSchemaKinds.Date,
            [typeof(DateTime)] = OxSchemaKinds.DateTime,
            [typeof(DateTimeOffset)] = OxSchemaKinds.DateTime,
            [typeof(TimeSpan)] = OxSchemaKinds.TimeSpan,
            [typeof(byte[])] = OxSchemaKinds.Binary,
            [typeof(TimeOnly)] = OxSchemaKinds.Unknown,
            [typeof(object)] = OxSchemaKinds.Unknown,
        };

        /// <summary>Namespaces holding serializer and runtime plumbing rather than model shape; a type in one is <c>unknown</c>.</summary>
        private static readonly string[] OpaqueNamespaces = ["MongoDB.Bson", "System.Text.Json", "System.Reflection", "System.IO"];

        private readonly Dictionary<Type, string> keys;
        private readonly Dictionary<string, OxSchemaType> pool;
        private readonly FindingCollector findings;
        private readonly IReadOnlyDictionary<string, string> entityIndex;

        /// <summary>One context for the whole walk; it caches per module and is used on one thread only.</summary>
        private readonly NullabilityInfoContext nullability = new();

        private int nextWorkingKey;

        public TypePoolWalker(
            Dictionary<Type, string> keys,
            Dictionary<string, OxSchemaType> pool,
            FindingCollector findings,
            IReadOnlyDictionary<string, string> entityIndex)
        {
            this.keys = keys;
            this.pool = pool;
            this.findings = findings;
            this.entityIndex = entityIndex;
        }

        /// <summary>
        /// The public instance properties of a type in the order the document publishes them:
        /// the most derived type first, then each base type in turn, and declaration order within
        /// a type.
        /// </summary>
        /// <remarks>
        /// The order is part of the serialised document and therefore of its revision, so it is
        /// defined here rather than taken from <see cref="Type.GetProperties()"/>, whose order the
        /// runtime does not guarantee. Declaration order is read off the metadata token, which the
        /// compiler assigns in source order.
        /// </remarks>
        public static IEnumerable<PropertyInfo> PublishedProperties(Type type)
        {
            for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
            {
                var declared = current
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .OrderBy(property => property.MetadataToken);

                foreach (var property in declared)
                    yield return property;
            }
        }

        /// <summary>The property list of a type: readable, non-indexed public instance properties, a shadowed name once.</summary>
        public IReadOnlyList<OxSchemaProperty> DescribeProperties(Type owner)
        {
            var label = LabelOf(owner);
            var declared = Relationships.DeclaredTargets(owner, label, EntityIdOf, findings);
            var properties = new List<OxSchemaProperty>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in PublishedProperties(owner))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                var name = EntityMetadata.WireNames.ConvertName(property.Name);

                if (!seen.Add(name))
                    continue;

                var descriptor = Describe(property.PropertyType, $"{label}#{name}") with
                {
                    Name = name,
                    Nullable = IsNullable(property),
                    StorageName = EntityMetadata.StorageNameOf(property.Name, name),
                    DisplayName = EntityMetadata.PropertyLabelOf(property.Name, name),
                };

                if (DescriptorVisitor.LeafKind(descriptor) == OxSchemaKinds.Guid)
                    descriptor = descriptor with { References = Relationships.Of(name, declared, entityIndex) };

                properties.Add(descriptor);
            }

            return properties;
        }

        /// <summary>The entity id of a type, or null when it is not an entity of this document.</summary>
        public string? EntityIdOf(Type type) =>
            keys.TryGetValue(type, out var key) && pool.TryGetValue(key, out var entry) && entry.Entity ? key : null;

        private string LabelOf(Type type) => EntityIdOf(type) ?? StructuralIds.ReadableId(type);

        /// <summary>
        /// Whether a client can read null out of a member: the annotation's read state, or what
        /// the runtime guarantees when the declaring assembly carries no annotations.
        /// </summary>
        private bool IsNullable(PropertyInfo property) =>
            nullability.Create(property).ReadState switch
            {
                NullabilityState.Nullable => true,
                NullabilityState.NotNull => false,
                _ => !property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) is not null,
            };

        /// <summary>Describes one member type. <c>Nullable&lt;T&gt;</c> is unwrapped first; composites recurse, scalars stop.</summary>
        private OxSchemaProperty Describe(Type type, string member)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type.IsEnum)
                return new OxSchemaProperty { Kind = OxSchemaKinds.Enum, Type = OxSchemaPointer.To(Pool(type)) };

            // A string is an IEnumerable<char> and a byte[] an IEnumerable<byte>, so scalars resolve first.
            if (ScalarKind(type) is { } scalar)
                return new OxSchemaProperty { Kind = scalar };

            if (TryGetDictionaryValueType(type, out var valueType, out var untypedDictionary))
            {
                if (untypedDictionary)
                    ReportUntyped(member, "dictionary");

                return new OxSchemaProperty { Kind = OxSchemaKinds.Dictionary, Value = Describe(valueType, member) };
            }

            if (TryGetElementType(type, out var elementType, out var untypedCollection))
            {
                if (untypedCollection)
                    ReportUntyped(member, "collection");

                return new OxSchemaProperty { Kind = OxSchemaKinds.Array, Of = Describe(elementType, member) };
            }

            var key = Pool(type);

            return new OxSchemaProperty
            {
                Kind = OxSchemaKinds.Object,
                Type = OxSchemaPointer.To(key),

                // An entity embedded in another type is a copy of a row another document owns.
                SnapshotOf = pool.TryGetValue(key, out var target) && target.Entity ? key : null,
            };
        }

        private void ReportUntyped(string member, string shape) =>
            findings.Add(
                OxSchemaCodes.CollectionUntyped,
                member,
                $"The {shape} declares no element type, so its values are described as unknown.");

        /// <summary>The pool key of a type, registering and describing it first when it is new.</summary>
        private string Pool(Type type)
        {
            if (keys.TryGetValue(type, out var existing))
                return existing;

            var key = $"working:{nextWorkingKey++}";

            keys[type] = key;
            pool[key] = type.IsEnum ? DescribeEnum(type) : DescribeObject(type);

            return key;
        }

        private OxSchemaType DescribeObject(Type type)
        {
            var properties = DescribeProperties(type);

            return new OxSchemaType { Properties = properties, Key = EntityMetadata.KeyOf(type, properties) };
        }

        /// <summary>
        /// An enum entry: its members in declaration order, each with its value and whether it is
        /// still active (not <c>[Obsolete]</c>). An enum has no property list at all.
        /// </summary>
        /// <remarks>
        /// Declaration order is read off the fields' metadata tokens and is inside the revision.
        /// <see cref="Enum.GetValuesAsUnderlyingType"/> is not a substitute: it sorts by value,
        /// collapses two names on one value, and loses the <c>[Obsolete]</c> marker.
        /// </remarks>
        private static OxSchemaType DescribeEnum(Type type) => new()
        {
            Kind = OxSchemaKinds.Enum,
            Flags = type.IsDefined(typeof(FlagsAttribute), inherit: false),
            Values =
            [
                .. type
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.IsLiteral)
                    .OrderBy(field => field.MetadataToken)
                    .Select(field => new OxSchemaEnumValue
                    {
                        Name = field.Name,
                        Value = ConstantValue(field),
                        Active = !field.IsDefined(typeof(ObsoleteAttribute), inherit: false),
                    }),
            ],
        };

        /// <summary>The declared value of an enum member as a signed 64-bit integer; a value past <c>long.MaxValue</c> wraps.</summary>
        private static long ConstantValue(FieldInfo field) =>
            field.GetRawConstantValue() switch
            {
                null => 0L,
                ulong unsigned => unchecked((long)unsigned),
                var raw => Convert.ToInt64(raw, CultureInfo.InvariantCulture),
            };

        /// <summary>The kind of a scalar leaf, or null for a composite to walk into.</summary>
        private static string? ScalarKind(Type type)
        {
            if (ScalarKinds.TryGetValue(type, out var kind))
                return kind;

            if (type.IsPrimitive || type.IsPointer || type.IsByRef || type.IsGenericParameter
                || typeof(Delegate).IsAssignableFrom(type)
                || typeof(Type).IsAssignableFrom(type)
                || IsOpaqueNamespace(type))
                return OxSchemaKinds.Unknown;

            return null;
        }

        private static bool IsOpaqueNamespace(Type type) =>
            type.Namespace is { } ns
            && OpaqueNamespaces.Any(opaque =>
                ns.Equals(opaque, StringComparison.Ordinal) || ns.StartsWith(opaque + ".", StringComparison.Ordinal));

        /// <summary>Whether the type is a dictionary, and its value type; a non-generic dictionary is untyped and its values are <c>object</c>.</summary>
        private static bool TryGetDictionaryValueType(Type type, out Type valueType, out bool untyped)
        {
            untyped = false;

            foreach (var candidate in Interfaces(type))
            {
                if (!candidate.IsGenericType)
                    continue;

                var definition = candidate.GetGenericTypeDefinition();

                if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
                {
                    valueType = candidate.GetGenericArguments()[1];
                    return true;
                }
            }

            valueType = typeof(object);
            untyped = typeof(IDictionary).IsAssignableFrom(type);

            return untyped;
        }

        /// <summary>Whether the type is a collection, and its element type; a non-generic collection is untyped and its elements are <c>object</c>.</summary>
        private static bool TryGetElementType(Type type, out Type elementType, out bool untyped)
        {
            untyped = false;

            if (type.IsArray)
            {
                elementType = type.GetElementType() ?? typeof(object);
                return true;
            }

            foreach (var candidate in Interfaces(type))
            {
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    elementType = candidate.GetGenericArguments()[0];
                    return true;
                }
            }

            elementType = typeof(object);
            untyped = typeof(IEnumerable).IsAssignableFrom(type);

            return untyped;
        }

        /// <summary>The type's interfaces, the type itself included when it is one.</summary>
        private static IEnumerable<Type> Interfaces(Type type)
        {
            if (type.IsInterface)
                yield return type;

            foreach (var candidate in type.GetInterfaces())
                yield return candidate;
        }
    }
}

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Options;
using OxQL.Model;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Fills the type pool from the query engine's entity model: one entry per pooled type,
    /// under the ids the model assigned, with every member described from the model's wire view.
    /// </summary>
    /// <remarks>
    /// The model is built once per host through the MongoDB driver's serializer registry, so
    /// the document describes exactly the types and members the engine binds against. What the
    /// document derives beyond the model (keys from the identity interfaces, storage-name and
    /// label exceptions) follows the rules of <see cref="EntityMetadata"/> and nothing else, so
    /// a byte of the document moves only when the model or one of those rules does.
    /// </remarks>
    internal static class TypePoolWalker
    {
        /// <summary>
        /// The pool of a model: enums with their values, structural types with their properties
        /// and keys, entities with their properties only (the builder adds the entity metadata).
        /// </summary>
        public static Dictionary<string, OxSchemaType> Project(EntityModel model)
        {
            var pool = new Dictionary<string, OxSchemaType>(StringComparer.Ordinal);

            foreach (var (id, type) in model.TypePool)
            {
                if (type.IsEnum)
                {
                    pool[id] = DescribeEnum(type);
                    continue;
                }

                var properties = DescribeProperties(type);

                var entry = type.IsEntity
                    ? new OxSchemaType { Entity = true, Properties = properties }
                    : new OxSchemaType { Properties = properties, Key = type.ClrType is null ? null : EntityMetadata.KeyOf(type.ClrType, properties) };

                pool[id] = entry with
                {
                    Description = type.Description,
                    Discriminator = DiscriminatorOf(type),
                    Variants = VariantsOf(type),
                    BaseVariant = BaseVariantOf(type),
                };
            }

            return pool;
        }

        /// <summary>The property list of a pooled type, in the model's member order.</summary>
        public static IReadOnlyList<OxSchemaProperty> DescribeProperties(TypeDef type)
        {
            var properties = new List<OxSchemaProperty>(type.Members.Count);

            foreach (var member in type.Members)
            {
                var clrName = member.ClrName ?? EntityMetadata.Pascalize(member.WireName);

                properties.Add(Describe(member) with
                {
                    Name = member.WireName,
                    Nullable = member.Nullable,
                    StorageName = EntityMetadata.StorageNameOf(clrName, member.WireName),
                    DisplayName = member.DisplayName,
                    Description = member.Description,
                    References = SimpleReference(member),
                    ReferenceCases = ReferenceCasesOf(member),
                    Constraints = ConstraintsOf(member.Constraints),
                    Deprecated = DeprecationOf(member.Deprecated),
                    OnlyFor = member.OnlyFor is { Count: > 0 } onlyFor ? [.. onlyFor] : null,
                    Stored = member.Stored ? null : false,
                    Relation = member.Relation is { } relation ? new OxSchemaRelation { Name = relation.Name, Member = relation.Member } : null,
                });
            }

            return properties;
        }

        /// <summary>Describes one shape: scalars stop, composites recurse, pointers name the pooled entry.</summary>
        private static OxSchemaProperty Describe(ShapeDef shape) => shape.Kind switch
        {
            Kind.Enum => new OxSchemaProperty { Kind = OxSchemaKinds.Enum, Type = Pointer(shape), StoredAs = StoredAs(shape) },
            Kind.Dictionary => new OxSchemaProperty { Kind = OxSchemaKinds.Dictionary, Value = shape.Value is null ? null : Describe(shape.Value), StoredAs = StoredAs(shape) },
            Kind.Array => new OxSchemaProperty { Kind = OxSchemaKinds.Array, Of = shape.Of is null ? null : Describe(shape.Of) },
            Kind.Object => new OxSchemaProperty { Kind = OxSchemaKinds.Object, Type = Pointer(shape), SnapshotOf = shape.SnapshotOf },
            var kind => new OxSchemaProperty { Kind = Kinds.NameOf(kind), StoredAs = StoredAs(shape) },
        };

        /// <summary>
        /// How a value is stored where its kind does not say it, which is exactly where the query
        /// engine treats it differently: a character stored as its code point has no text operators,
        /// a scalar stored as a document is not filterable, and a dictionary stored as an array is a
        /// collection. Null for every representation a kind implies.
        /// </summary>
        private static string? StoredAs(ShapeDef shape) => shape switch
        {
            { Kind: Kind.Dictionary, DictionaryRepresentation: DictionaryRepresentation.ArrayOfDocuments } => OxSchemaStoredAs.ArrayOfDocuments,
            { Kind: Kind.Dictionary, DictionaryRepresentation: DictionaryRepresentation.ArrayOfArrays } => OxSchemaStoredAs.ArrayOfArrays,
            { Kind: Kind.Dictionary } => null,
            { Kind: Kind.String, Representation.BsonType: BsonType.Int32 } => OxSchemaStoredAs.CodePoint,
            { Representation.BsonType: BsonType.Document } when Kinds.IsScalar(shape.Kind) => OxSchemaStoredAs.Document,
            _ => null,
        };

        private static string? Pointer(ShapeDef shape) => shape.Type is null ? null : OxSchemaPointer.To(shape.Type.PoolId);

        /// <summary>
        /// The <c>references</c> member: only a simple reference, one unconditional entity target
        /// with no item path and no key conversion. A format 1.0 reader maps every
        /// <c>references</c> to an entity join, so nothing else may reach it.
        /// </summary>
        private static OxSchemaReference? SimpleReference(MemberDef member) =>
            member.References is [{ IsSimple: true } reference]
                // A declared reference is exactly what makes a lookup or a resolve
                // legal under contract 2; inference is gone, so nothing is inferred.
                ? new OxSchemaReference { Entity = reference.TargetEntity, Field = reference.TargetField, Joinable = true, Inferred = false }
                : null;

        /// <summary>Every case of a reference that is not simple, in the model's order; null for none or a simple one.</summary>
        private static IReadOnlyList<OxSchemaReferenceCase>? ReferenceCasesOf(MemberDef member) =>
            member.References.Count == 0 || member.References is [{ IsSimple: true }]
                ? null
                : [.. member.References.Select(CaseOf)];

        private static OxSchemaReferenceCase CaseOf(ReferenceDef reference) => new()
        {
            When = reference.When switch
            {
                ReferenceCondition.PathEquals condition => new OxSchemaReferenceCondition { Path = condition.Path, EqualsAny = [.. condition.Values] },
                ReferenceCondition.Variant condition => new OxSchemaReferenceCondition { Variant = [.. condition.Names] },
                _ => null,
            },
            KeyAs = reference.KeyAs == KeyAs.Guid ? OxSchemaKeyAs.Guid : null,
            Targets = [.. reference.Targets.Select(target => new OxSchemaReferenceTarget { Entity = target.Entity, Item = target.Item, Field = target.Field })],
        };

        private static OxSchemaConstraints? ConstraintsOf(ConstraintsDef? constraints) => constraints is null
            ? null
            : new OxSchemaConstraints { MaxLength = constraints.MaxLength, Min = constraints.Min, Max = constraints.Max, Pattern = constraints.Pattern };

        private static OxSchemaDeprecation? DeprecationOf(DeprecationDef? deprecated) => deprecated is null
            ? null
            : new OxSchemaDeprecation { Since = deprecated.Since, ReplacedBy = deprecated.ReplacedBy, Note = deprecated.Note };

        /// <summary>The discriminator of a type with variants; null on every other type.</summary>
        private static OxSchemaDiscriminator? DiscriminatorOf(TypeDef type) => type.Variants.Count == 0
            ? null
            : new OxSchemaDiscriminator
            {
                Element = type.DiscriminatorElement ?? "_t",
                Form = type.DiscriminatorForm == DiscriminatorForm.Hierarchical ? OxSchemaDiscriminatorForms.Hierarchical : OxSchemaDiscriminatorForms.Scalar,
            };

        /// <summary>
        /// The name a value stored as a polymorphic type itself goes by: the class name without its
        /// generic arity when the class is concrete, as the query engine's <c>is</c> accepts it; null
        /// for an abstract class, an interface and a type without variants.
        /// </summary>
        private static string? BaseVariantOf(TypeDef type)
        {
            if (type.Variants.Count == 0 || type.ClrType is not { IsAbstract: false, IsInterface: false } clr)
                return null;

            var arity = clr.Name.IndexOf('`', StringComparison.Ordinal);

            return arity > 0 ? clr.Name[..arity] : clr.Name;
        }

        /// <summary>The variants of a type, ordinally by name, each pointing at its pooled entry; null when it has none.</summary>
        private static IReadOnlyList<OxSchemaVariant>? VariantsOf(TypeDef type) => type.Variants.Count == 0
            ? null
            : [.. type.Variants
                .OrderBy(variant => variant.Name, StringComparer.Ordinal)
                .Select(variant => new OxSchemaVariant { Name = variant.Name, Type = OxSchemaPointer.To(variant.Type.PoolId) })];

        /// <summary>An enum entry: its members in declaration order, each with its value and whether it is still active.</summary>
        private static OxSchemaType DescribeEnum(TypeDef type) => new()
        {
            Kind = OxSchemaKinds.Enum,
            Description = type.Description,
            Flags = type.EnumFlags,
            Values = [.. type.EnumValues.Select(value => new OxSchemaEnumValue { Name = value.Name, Value = value.Value, Active = value.Active, Description = value.Description })],
        };
    }
}

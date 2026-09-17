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
    /// label exceptions) keeps the rules of <see cref="EntityMetadata"/>, so the published bytes
    /// move only where the model itself differs from the former CLR walk.
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

                pool[id] = type.IsEntity
                    ? new OxSchemaType { Entity = true, Properties = properties }
                    : new OxSchemaType { Properties = properties, Key = type.ClrType is null ? null : EntityMetadata.KeyOf(type.ClrType, properties) };
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
                    References = member.Reference is { } reference
                        ? new OxSchemaReference { Entity = reference.TargetEntity, Field = reference.TargetField, Joinable = false, Inferred = false }
                        : null,
                });
            }

            return properties;
        }

        /// <summary>Describes one shape: scalars stop, composites recurse, pointers name the pooled entry.</summary>
        private static OxSchemaProperty Describe(ShapeDef shape) => shape.Kind switch
        {
            Kind.Enum => new OxSchemaProperty { Kind = OxSchemaKinds.Enum, Type = Pointer(shape) },
            Kind.Dictionary => new OxSchemaProperty { Kind = OxSchemaKinds.Dictionary, Value = shape.Value is null ? null : Describe(shape.Value) },
            Kind.Array => new OxSchemaProperty { Kind = OxSchemaKinds.Array, Of = shape.Of is null ? null : Describe(shape.Of) },
            Kind.Object => new OxSchemaProperty { Kind = OxSchemaKinds.Object, Type = Pointer(shape), SnapshotOf = shape.SnapshotOf },
            var kind => new OxSchemaProperty { Kind = Kinds.NameOf(kind) },
        };

        private static string? Pointer(ShapeDef shape) => shape.Type is null ? null : OxSchemaPointer.To(shape.Type.PoolId);

        /// <summary>An enum entry: its members in declaration order, each with its value and whether it is still active.</summary>
        private static OxSchemaType DescribeEnum(TypeDef type) => new()
        {
            Kind = OxSchemaKinds.Enum,
            Flags = type.EnumFlags,
            Values = [.. type.EnumValues.Select(value => new OxSchemaEnumValue { Name = value.Name, Value = value.Value, Active = value.Active })],
        };
    }
}

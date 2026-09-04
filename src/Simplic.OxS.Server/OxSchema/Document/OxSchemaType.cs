using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>One entry of the type pool. An entity is a structural type that additionally carries entity metadata.</summary>
    public sealed record OxSchemaType
    {
        /// <summary><c>enum</c> on an enum entry; absent on an object entry.</summary>
        [JsonPropertyOrder(0)]
        public string? Kind { get; init; }

        /// <summary>The human label of an entity. Absent on a structural type.</summary>
        [JsonPropertyOrder(1)]
        public string? DisplayName { get; init; }

        /// <summary>A description, when the model declares one.</summary>
        [JsonPropertyOrder(2)]
        public string? Description { get; init; }

        /// <summary>Whether an enum entry is a flags enum. Absent on a non-enum entry.</summary>
        [JsonPropertyOrder(3)]
        public bool? Flags { get; init; }

        /// <summary>The value list of an enum entry, in declaration order. Absent on a non-enum entry.</summary>
        [JsonPropertyOrder(4)]
        public IReadOnlyList<OxSchemaEnumValue>? Values { get; init; }

        /// <summary>True on an entity entry; absent on a structural type.</summary>
        [JsonPropertyOrder(5)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Entity { get; init; }

        /// <summary>The ids this entity is also known by: the ids it retired, then the legacy <c>$ClassName</c> model ids its controller publishes. Entity-only.</summary>
        [JsonPropertyOrder(6)]
        public IReadOnlyList<string>? Aliases { get; init; }

        /// <summary>The property paths that identify an instance. Present on entities and on keyed item types.</summary>
        [JsonPropertyOrder(7)]
        public IReadOnlyList<string>? Key { get; init; }

        /// <summary>The property that names an instance. Entity-only, absent when no candidate exists.</summary>
        [JsonPropertyOrder(8)]
        public string? Display { get; init; }

        /// <summary>Whether the entity accepts an organisation's declared addon fields. Entity-only.</summary>
        [JsonPropertyOrder(9)]
        public bool? Extendable { get; init; }

        /// <summary>True on an entity entry: every entity in the pool is accepted as a query's entity type.</summary>
        [JsonPropertyOrder(10)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Queryable { get; init; }

        /// <summary>Paths the entity refuses to filter on. Entity-only, always present, possibly empty.</summary>
        [JsonPropertyOrder(11)]
        public IReadOnlyList<string>? NotFilterable { get; init; }

        /// <summary>Paths the entity refuses to sort on. Entity-only, always present, possibly empty.</summary>
        [JsonPropertyOrder(12)]
        public IReadOnlyList<string>? NotSortable { get; init; }

        /// <summary>
        /// The REST operations of an entity by slot (<c>get</c>, <c>create</c>, <c>update</c>, <c>replace</c>,
        /// <c>delete</c>), sorted ordinally. Absent when no linked controller routes any slot.
        /// </summary>
        [JsonPropertyOrder(13)]
        public ImmutableSortedDictionary<string, OxSchemaOperation>? Operations { get; init; }

        /// <summary>Every path under an entity whose terminal is an array of a keyed item type. Entity-only, always present, possibly empty.</summary>
        [JsonPropertyOrder(14)]
        public IReadOnlyList<OxSchemaEntityItem>? Items { get; init; }

        /// <summary>The property list. Absent on an enum entry; an object entry always carries it, empty included.</summary>
        [JsonPropertyOrder(15)]
        public IReadOnlyList<OxSchemaProperty>? Properties { get; init; }
    }

    /// <summary>One member of an enum entry.</summary>
    public sealed record OxSchemaEnumValue
    {
        /// <summary>The CLR member name, verbatim.</summary>
        [JsonPropertyOrder(0)]
        public required string Name { get; init; }

        /// <summary>The declared value, as a JSON number. A reader must accept a JSON string too.</summary>
        [JsonPropertyOrder(1)]
        public required long Value { get; init; }

        /// <summary>False retires a member without breaking historical data.</summary>
        [JsonPropertyOrder(2)]
        public required bool Active { get; init; }
    }

    /// <summary>One item collection of an entity: the path that reaches it and its legacy ids.</summary>
    public sealed record OxSchemaEntityItem
    {
        /// <summary>A dotted path of wire segments; array traversal is implicit.</summary>
        [JsonPropertyOrder(0)]
        public required string Path { get; init; }

        /// <summary>The two-part <c>$Parent.$Child</c> legacy model ids of this collection; only ids the service's legacy document publishes. Always present, possibly empty.</summary>
        [JsonPropertyOrder(1)]
        public required IReadOnlyList<string> Aliases { get; init; }
    }

    /// <summary>One REST operation of an entity: a real HTTP verb and an app-relative route.</summary>
    public sealed record OxSchemaOperation
    {
        /// <summary>The HTTP verb, upper-case.</summary>
        [JsonPropertyOrder(0)]
        public required string Method { get; init; }

        /// <summary>The route below the service's API base path, e.g. <c>/Vehicle/{id}</c>.</summary>
        [JsonPropertyOrder(1)]
        public required string Route { get; init; }
    }

    /// <summary>A foreign key: the entity a guid property points at.</summary>
    public sealed record OxSchemaReference
    {
        /// <summary>The target entity id.</summary>
        [JsonPropertyOrder(0)]
        public required string Entity { get; init; }

        /// <summary>The single path that is the target's key. Absent when the target is not an entity of this document or its key is not one path.</summary>
        [JsonPropertyOrder(1)]
        public string? Field { get; init; }

        /// <summary>Whether a query may join on this reference.</summary>
        [JsonPropertyOrder(2)]
        public required bool Joinable { get; init; }

        /// <summary>True when the target was derived from the property name rather than declared.</summary>
        [JsonPropertyOrder(3)]
        public required bool Inferred { get; init; }
    }
}

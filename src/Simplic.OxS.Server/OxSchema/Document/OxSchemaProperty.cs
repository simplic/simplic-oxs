using System.Text.Json.Serialization;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// A property descriptor. On a type's property list it describes a member; nested as an
    /// array's <see cref="Of"/> or a dictionary's <see cref="Value"/> it describes a shape and
    /// carries no member facts.
    /// </summary>
    public sealed record OxSchemaProperty
    {
        /// <summary>The camelCase wire name. Absent on a nested descriptor.</summary>
        [JsonPropertyOrder(0)]
        public string? Name { get; init; }

        /// <summary>
        /// The name the member is stored and queried under, present only where it is not
        /// <see cref="Name"/> with its first letter upper-cased. A query uses the wire spelling;
        /// this is for consumers that address storage themselves, where the derivation is wrong
        /// exactly on acronym runs (<c>qrCode</c> is stored as <c>QRCode</c>).
        /// </summary>
        [JsonPropertyOrder(1)]
        public string? StorageName { get; init; }

        /// <summary>One of <see cref="OxSchemaKinds"/>.</summary>
        [JsonPropertyOrder(2)]
        public required string Kind { get; init; }

        /// <summary>A pointer into the pool, <c>#/types/&lt;id&gt;</c>. Present on object and enum kinds.</summary>
        [JsonPropertyOrder(3)]
        public string? Type { get; init; }

        /// <summary>The element descriptor of an array.</summary>
        [JsonPropertyOrder(4)]
        public OxSchemaProperty? Of { get; init; }

        /// <summary>The value descriptor of a dictionary.</summary>
        [JsonPropertyOrder(5)]
        public OxSchemaProperty? Value { get; init; }

        /// <summary>Whether a client can read null out of the member. Absent on a nested descriptor.</summary>
        [JsonPropertyOrder(6)]
        public bool? Nullable { get; init; }

        /// <summary>
        /// The human label, present only where it is not the de-camelCased <see cref="Name"/>,
        /// which again is the acronym case (<c>qrCode</c> labels as "QR Code", not "Qr Code").
        /// </summary>
        [JsonPropertyOrder(7)]
        public string? DisplayName { get; init; }

        /// <summary>A description, when the model declares one.</summary>
        [JsonPropertyOrder(8)]
        public string? Description { get; init; }

        /// <summary>
        /// The entity this member is an embedded copy of. Travels with the pointer, so it appears
        /// on a nested descriptor too. A copy is never joinable.
        /// </summary>
        [JsonPropertyOrder(9)]
        public string? SnapshotOf { get; init; }

        /// <summary>The foreign key this member is, when it is one.</summary>
        [JsonPropertyOrder(10)]
        public OxSchemaReference? References { get; init; }

        /// <summary>Value constraints, when the model declares any.</summary>
        [JsonPropertyOrder(11)]
        public OxSchemaConstraints? Constraints { get; init; }

        /// <summary>The member's deprecation, when the model declares one.</summary>
        [JsonPropertyOrder(12)]
        public OxSchemaDeprecation? Deprecated { get; init; }

        /// <summary>
        /// A closed value list with labels. Only an addon definition descriptor
        /// (<c>GET /schema/addons</c>) carries it; never a member of the schema document.
        /// </summary>
        [JsonPropertyOrder(13)]
        public IReadOnlyList<OxSchemaValue>? Values { get; init; }

        /// <summary>
        /// The variants of the holding type that carry this member, when not all of them do
        /// (format 1.1). A reader treats the member as absent on every other stored value.
        /// </summary>
        [JsonPropertyOrder(14)]
        public IReadOnlyList<string>? OnlyFor { get; init; }

        /// <summary>
        /// Every case of a reference that is not simple, in declaration order (format 1.1): a
        /// condition, an item target, a key conversion or several targets. Never beside
        /// <see cref="References"/>.
        /// </summary>
        [JsonPropertyOrder(15)]
        public IReadOnlyList<OxSchemaReferenceCase>? ReferenceCases { get; init; }
    }

    /// <summary>One case of a reference (format 1.1). A case without <see cref="When"/> applies unconditionally.</summary>
    public sealed record OxSchemaReferenceCase
    {
        /// <summary>The condition the case applies under.</summary>
        [JsonPropertyOrder(0)]
        public OxSchemaReferenceCondition? When { get; init; }

        /// <summary>How the stored value becomes the targets' key; <c>guid</c>, or absent for no conversion.</summary>
        [JsonPropertyOrder(1)]
        public string? KeyAs { get; init; }

        /// <summary>The targets, in the order they are tried.</summary>
        [JsonPropertyOrder(2)]
        public required IReadOnlyList<OxSchemaReferenceTarget> Targets { get; init; }
    }

    /// <summary>
    /// The condition of a reference case: either a sibling <see cref="Path"/> holding one of
    /// <see cref="EqualsAny"/>, or the holding object being stored as one of <see cref="Variant"/>.
    /// </summary>
    public sealed record OxSchemaReferenceCondition
    {
        /// <summary>The sibling's wire name. Absent on a variant condition.</summary>
        [JsonPropertyOrder(0)]
        public string? Path { get; init; }

        /// <summary>The values the sibling is compared with, exactly. Absent on a variant condition.</summary>
        [JsonPropertyOrder(1)]
        [JsonPropertyName("equals")]
        public IReadOnlyList<string>? EqualsAny { get; init; }

        /// <summary>The variant names the holding object is tested against. Absent on a path condition.</summary>
        [JsonPropertyOrder(2)]
        public IReadOnlyList<string>? Variant { get; init; }
    }

    /// <summary>One target of a reference case.</summary>
    public sealed record OxSchemaReferenceTarget
    {
        /// <summary>The target entity id; it may belong to another service.</summary>
        [JsonPropertyOrder(0)]
        public required string Entity { get; init; }

        /// <summary>The path of the target's array whose element the value names. Absent when the value names the entity itself.</summary>
        [JsonPropertyOrder(1)]
        public string? Item { get; init; }

        /// <summary>The path the value matches: on the element when <see cref="Item"/> is present, else on the entity.</summary>
        [JsonPropertyOrder(2)]
        public string? Field { get; init; }
    }

    /// <summary>The key conversions of a reference case.</summary>
    public static class OxSchemaKeyAs
    {
        /// <summary>A string member holding a guid.</summary>
        public const string Guid = "guid";
    }

    /// <summary>One entry of a closed value list.</summary>
    public sealed record OxSchemaValue
    {
        /// <summary>The value, as the bag stores it: a string, or the decimal digits of an int.</summary>
        [JsonPropertyOrder(0)]
        public required string Value { get; init; }

        /// <summary>The label shown for the value.</summary>
        [JsonPropertyOrder(1)]
        public string? Label { get; init; }
    }

    /// <summary>Value constraints of a property. Bounds travel as strings because a JSON number is a double.</summary>
    public sealed record OxSchemaConstraints
    {
        /// <summary>The longest string the member accepts.</summary>
        [JsonPropertyOrder(0)]
        public int? MaxLength { get; init; }

        /// <summary>The smallest value the member accepts.</summary>
        [JsonPropertyOrder(1)]
        public string? Min { get; init; }

        /// <summary>The largest value the member accepts.</summary>
        [JsonPropertyOrder(2)]
        public string? Max { get; init; }

        /// <summary>A regular expression the member's value satisfies.</summary>
        [JsonPropertyOrder(3)]
        public string? Pattern { get; init; }
    }

    /// <summary>A member's deprecation.</summary>
    public sealed record OxSchemaDeprecation
    {
        /// <summary>The version the member was deprecated in.</summary>
        [JsonPropertyOrder(0)]
        public string? Since { get; init; }

        /// <summary>The path that replaces it.</summary>
        [JsonPropertyOrder(1)]
        public string? ReplacedBy { get; init; }

        /// <summary>A note for the reader.</summary>
        [JsonPropertyOrder(2)]
        public string? Note { get; init; }
    }

    /// <summary>
    /// Pointer syntax for the type pool. Pool keys are bare ids; only a pointer carries the
    /// <c>#/types/</c> prefix, for structural and entity targets alike.
    /// </summary>
    public static class OxSchemaPointer
    {
        /// <summary>The prefix every pointer carries.</summary>
        public const string Prefix = "#/types/";

        /// <summary>Wraps a bare pool key as a pointer.</summary>
        public static string To(string id) => Prefix + id;

        /// <summary>Unwraps a pointer to its pool key. A bare key passes through.</summary>
        public static string Strip(string pointer) =>
            pointer.StartsWith(Prefix, StringComparison.Ordinal) ? pointer[Prefix.Length..] : pointer;
    }

    /// <summary>The kind vocabulary. <c>unknown</c> is explicit; nothing degrades to <c>object</c>.</summary>
    public static class OxSchemaKinds
    {
        /// <summary>A string.</summary>
        public const string String = "string";

        /// <summary>A 32-bit or narrower integer.</summary>
        public const string Int = "int";

        /// <summary>A 64-bit integer, a JSON string on the wire.</summary>
        public const string Long = "long";

        /// <summary>A decimal, a JSON string on the wire.</summary>
        public const string Decimal = "decimal";

        /// <summary>A floating-point number.</summary>
        public const string Double = "double";

        /// <summary>A boolean.</summary>
        public const string Bool = "bool";

        /// <summary>A GUID string.</summary>
        public const string Guid = "guid";

        /// <summary>A calendar date, <c>YYYY-MM-DD</c> on the wire.</summary>
        public const string Date = "date";

        /// <summary>A date and time, ISO-8601 on the wire.</summary>
        public const string DateTime = "dateTime";

        /// <summary>A duration, ISO-8601 on the wire.</summary>
        public const string TimeSpan = "timeSpan";

        /// <summary>An enum; the descriptor points at the pooled enum entry.</summary>
        public const string Enum = "enum";

        /// <summary>Binary data, base64 on the wire.</summary>
        public const string Binary = "binary";

        /// <summary>A member whose shape the document cannot describe.</summary>
        public const string Unknown = "unknown";

        /// <summary>An object; the descriptor points at the pooled entry.</summary>
        public const string Object = "object";

        /// <summary>An array; the descriptor carries the element descriptor.</summary>
        public const string Array = "array";

        /// <summary>A dictionary with tenant-controlled keys; the descriptor carries the value descriptor.</summary>
        public const string Dictionary = "dictionary";
    }
}

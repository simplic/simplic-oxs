using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;
using OxQL.Core.Attributes;
using OxQL.Model.Attributes;
using Simplic.OxS.Data;

namespace Simplic.OxS.Server.Test.OxSchema.Fixtures
{
    /// <summary>
    /// An entity carrying every format 1.1 member: descriptions, constraints, a deprecation,
    /// a polymorphic collection, typed reference cases and an item reference.
    /// </summary>
    [OxQLType("probe.ledger", "probe.ledger")]
    [Description("A ledger of entries.")]
    public class LedgerModel : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        [OxQLDescription("The ledger's name.")]
        [MaxLength(40)]
        public string? Name { get; set; }

        [Obsolete("Use name.")]
        public string? Title { get; set; }

        [Range(1, 10)]
        public int Rank { get; set; }

        [RegularExpression("^[A-Z]+$")]
        public string? Code { get; set; }

        public LedgerKind Kind { get; set; }

        /// <summary>A collection of a polymorphic item type.</summary>
        public List<Entry> Entries { get; set; } = [];

        /// <summary>A conditional reference whose cases name an entity and an item of another one.</summary>
        public SourceReference? Source { get; set; }

        /// <summary>The discriminating sibling of <see cref="ReferenceId"/>.</summary>
        public string? DataType { get; set; }

        /// <summary>A string member holding a guid under one condition.</summary>
        [OxQLReferenceWhen("dataType", "thing", "probe.thing", KeyAs = OxQLKeyAs.Guid)]
        public string? ReferenceId { get; set; }

        /// <summary>An unconditional reference to an item: never a simple reference.</summary>
        [OxQLReference("probe.widget", "id", Item = "slots")]
        public Guid SlotId { get; set; }

        /// <summary>A shared type the service cannot annotate; the host declares its reference.</summary>
        public Party? Owner { get; set; }

        /// <summary>One character, which the driver stores as its code point.</summary>
        public char Grade { get; set; }

        /// <summary>A dictionary the driver stores as an array of key and value documents.</summary>
        [BsonDictionaryOptions(MongoDB.Bson.Serialization.Options.DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, int> Tally { get; set; } = [];

        /// <summary>A scalar the driver stores as a document.</summary>
        [BsonRepresentation(MongoDB.Bson.BsonType.Document)]
        public DateTimeOffset Stamp { get; set; }
    }

    /// <summary>An enum with a type description and a described member.</summary>
    [OxQLDescription("How a ledger is kept.")]
    public enum LedgerKind
    {
        [Description("A cash book.")]
        Cash = 0,

        Bank = 1,
    }

    /// <summary>The base of a polymorphic item; its variants are registered through the known types.</summary>
    [BsonKnownTypes(typeof(LineEntry), typeof(GroupEntry))]
    public class Entry
    {
        public Guid Id { get; set; }

        public string? Note { get; set; }

        /// <summary>The model's own type member: a constant of each class, returned and never stored.</summary>
        public virtual string Kind => "entry";
    }

    /// <summary>A variant carrying a reference only it has.</summary>
    public class LineEntry : Entry
    {
        public override string Kind => "line";

        [OxQLReference("probe.thing")]
        public Guid ThingId { get; set; }
    }

    /// <summary>A variant that nests entries of the base type.</summary>
    public class GroupEntry : Entry
    {
        public override string Kind => "group";

        public List<Entry> Items { get; set; } = [];
    }

    /// <summary>A reference whose target depends on a sibling.</summary>
    public class SourceReference
    {
        public string? Type { get; set; }

        [OxQLReferenceWhen("type", "widget", "probe.widget#slots", Field = "id")]
        [OxQLReferenceWhen("type", "thing", "probe.thing")]
        public Guid Id { get; set; }
    }

    /// <summary>A type the host declares references on, as if it came from a shared package.</summary>
    public class Party
    {
        public Guid Id { get; set; }

        public string? Name { get; set; }

        /// <summary>The discriminating sibling of <see cref="ExternalId"/>.</summary>
        public string? ExternalKind { get; set; }

        /// <summary>A string member the host declares a converted reference on.</summary>
        public string? ExternalId { get; set; }
    }
}

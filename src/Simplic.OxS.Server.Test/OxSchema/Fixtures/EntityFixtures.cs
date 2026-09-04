using OxQL.Core.Attributes;
using Simplic.OxS.Data;
using Simplic.OxS.ModelDefinition.Extenstion.Abstractions;

namespace Simplic.OxS.Server.Test.OxSchema.Fixtures
{
    /// <summary>
    /// An extendable entity whose members cover the naming exceptions and the item collections.
    /// </summary>
    [OxQLType("probe.widget", "probe.widget", Extendable = true)]
    public class WidgetModel : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        /// <summary>A member with no setter, described as the plain string it is.</summary>
        public string Label => "computed";

        /// <summary>The acronym run: the wire name loses a boundary the storage name keeps.</summary>
        public string? QRCode { get; set; }

        /// <summary>The ordinary case beside the acronym run: neither exception member is emitted.</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Named so this entity has no display candidate at all.</summary>
        public string? Caption { get; set; }

        /// <summary>A collection of a keyed item type.</summary>
        public List<Slot> Slots { get; set; } = [];

        /// <summary>A collection of an unkeyed item type.</summary>
        public List<Tag> Tags { get; set; } = [];
    }

    /// <summary>
    /// An entity that implements no identity interface, so a property named Id gives it no key.
    /// </summary>
    [OxQLType("probe.thing", "probe.thing")]
    public class Thing
    {
        public Guid Id { get; set; }

        public Mode Mode { get; set; }

        /// <summary>Resolves to the same pooled enum as <see cref="Mode"/>, nullable at the member.</summary>
        public Mode? OptionalMode { get; set; }

        public Access Access { get; set; }

        public Retired Retired { get; set; }

        public Wide Wide { get; set; }

        /// <summary>An item collection whose two-part legacy id resolves.</summary>
        public List<Slot> Slots { get; set; } = [];

        /// <summary>An enum one level below the member, so the pointer sits on the element.</summary>
        public List<Mode> Modes { get; set; } = [];
    }

    /// <summary>One of two entities whose ids share a last segment.</summary>
    [OxQLType("probe.gadget", "probe.gadget")]
    public class Gadget : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>The other of two entities whose ids share a last segment.</summary>
    [OxQLType("spare.gadget", "spare.gadget")]
    public class SpareGadget : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>Every relationship shape in one entity.</summary>
    [OxQLType("probe.link", "probe.link")]
    public class Link : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public string? Name { get; set; }

        /// <summary>A declaration whose navigation type is an entity and whose named property exists.</summary>
        [ReferenceId("ThingId")]
        public Thing? Thing { get; set; }

        public Guid ThingId { get; set; }

        /// <summary>A stem that is an entity id's last segment, so the name alone resolves it.</summary>
        public Guid WidgetId { get; set; }

        /// <summary>A stem that only ends with an entity segment, which resolves nothing.</summary>
        public Guid? OtherThingId { get; set; }

        /// <summary>The Guid suffix, which resolves like the Id suffix.</summary>
        public Guid ThingGuid { get; set; }

        /// <summary>A stem two entity ids end with, so it resolves to neither.</summary>
        public Guid GadgetId { get; set; }

        /// <summary>A stem that names no entity.</summary>
        public Guid RegistratorId { get; set; }

        /// <summary>A declaration whose navigation type is not an entity.</summary>
        [ReferenceId("SubsetId")]
        public ThingSubset? Subset { get; set; }

        public Guid SubsetId { get; set; }

        /// <summary>A declaration naming a property this type does not have.</summary>
        [ReferenceId("MissingId")]
        public Thing? Absent { get; set; }

        /// <summary>An entity embedded as a single object.</summary>
        public Thing? Single { get; set; }

        /// <summary>An entity embedded as an array element.</summary>
        public List<Thing> Many { get; set; } = [];

        /// <summary>An entity embedded as a dictionary value.</summary>
        public Dictionary<string, Thing> Keyed { get; set; } = [];

        /// <summary>An owned embedded shape, which is neither a snapshot nor a reference.</summary>
        public List<Slot> Slots { get; set; } = [];

        /// <summary>One of two pooled types with one CLR name.</summary>
        public Alpha.Detail? Left { get; set; }

        /// <summary>The other of two pooled types with one CLR name.</summary>
        public Beta.Detail? Right { get; set; }

        /// <summary>One of two closures of one generic.</summary>
        public Pair<string>? Names { get; set; }

        /// <summary>The other of two closures of one generic.</summary>
        public Pair<int>? Numbers { get; set; }
    }

    /// <summary>An entity whose id has a single segment, which is off the id grammar and still resolves.</summary>
    [OxQLType("badid", "badid")]
    public class BadId : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>One of two declarations of one id, differing only in case.</summary>
    [OxQLType("probe.twin", "probe.twin")]
    public class TwinA : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>The other declaration of that id.</summary>
    [OxQLType("Probe.Twin", "probe.twin")]
    public class TwinB : IDocument<Guid>
    {
        public Guid Id { get; set; }

        public bool IsDeleted { get; set; }

        public string? Name { get; set; }
    }
}

using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// One organisation's definition of a key under an extendable entity's addon bag: a hint
/// that the value at the path is most likely of the given kind. Nothing is refused on write
/// and nothing is normalised; the query engine matches tolerantly on the read side.
/// </summary>
public class AddonDefinitionDocument : OrganizationDocumentBase
{
    /// <summary>
    /// Gets or sets the extendable entity id the key lives under (e.g. "logistics.shipment").
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage path under the bag, verbatim and dot-separated; segments may
    /// contain spaces and the driver's <c>_t</c>/<c>_v</c> wrappers are ordinary segments.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind, in the schema's spelling: string, int, long, double, decimal,
    /// bool, date, dateTime, guid, or object for an explicit container with no typing of its own.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional closed value list of a string or int key.
    /// </summary>
    public List<AddonDefinitionValue>? Values { get; set; }

    /// <summary>
    /// Gets or sets the human label.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether the definition is retired: the key is opaque again, the row stays.
    /// </summary>
    public bool Retired { get; set; }
}

/// <summary>
/// One entry of a closed value list.
/// </summary>
public class AddonDefinitionValue
{
    /// <summary>
    /// Gets or sets the value, as the bag stores it (a string, or the decimal digits of an int).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the label shown for the value.
    /// </summary>
    public string? Label { get; set; }
}

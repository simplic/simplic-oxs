namespace Simplic.OxS.Server.Controllers.Model;

/// <summary>
/// Request model for creating an addon definition. Path and kind are immutable afterwards.
/// </summary>
public class CreateAddonDefinitionRequest
{
    /// <summary>
    /// Gets or sets the extendable entity id the key lives under (e.g. "logistics.shipment").
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage path under the addon bag, verbatim and dot-separated.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind: string, int, long, double, decimal, bool, date, dateTime, guid, or object.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional closed value list of a string or int key.
    /// </summary>
    public List<AddonDefinitionValueRequest>? Values { get; set; }

    /// <summary>
    /// Gets or sets the human label.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Request model for updating the labels and the value list of an addon definition.
/// </summary>
public class UpdateAddonDefinitionRequest
{
    /// <summary>
    /// Gets or sets the closed value list; null clears it.
    /// </summary>
    public List<AddonDefinitionValueRequest>? Values { get; set; }

    /// <summary>
    /// Gets or sets the human label.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// One entry of a closed value list.
/// </summary>
public class AddonDefinitionValueRequest
{
    /// <summary>
    /// Gets or sets the value, as the bag stores it.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the label shown for the value.
    /// </summary>
    public string? Label { get; set; }
}

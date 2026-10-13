using System.ComponentModel;

namespace Simplic.OxS.Server.Controllers.Model;

/// <summary>
/// Request model for creating an addon definition. Path and kind are immutable afterwards.
/// </summary>
public class CreateAddonDefinitionRequest
{
    /// <summary>
    /// Gets or sets the extendable entity id the key lives under (e.g. "logistics.shipment").
    /// </summary>
    [Description("The id of the extendable entity the property is added to, as the schema document names it (`parcel.parcel`).")]
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage path under the addon bag, verbatim and dot-separated.
    /// </summary>
    [Description("The key of the property under the entity's `addon` member, as it is stored; dots separate the segments of a nested key (`handling`, `customs.code`). It does not change afterwards.")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind: string, int, long, double, decimal, bool, date, dateTime, guid, or object.
    /// </summary>
    [Description("The kind the property is filtered and sorted as: `string`, `int`, `long`, `double`, `decimal`, `bool`, `date`, `dateTime`, `guid` or `object`. It does not change afterwards.")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional closed value list of a string or int key.
    /// </summary>
    [Description("The values the property takes, where it takes no other: only for a `string` or an `int` property. Left out, the property takes every value of its kind.")]
    public List<AddonDefinitionValueRequest>? Values { get; set; }

    /// <summary>
    /// Gets or sets the human label.
    /// </summary>
    [Description("The label the property is shown with.")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    [Description("What the property means, for the people who use it.")]
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
    [Description("The values the property takes from now on; left out or `null`, the property takes every value of its kind.")]
    public List<AddonDefinitionValueRequest>? Values { get; set; }

    /// <summary>
    /// Gets or sets the human label.
    /// </summary>
    [Description("The label the property is shown with; left out or `null`, it has none.")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    [Description("What the property means; left out or `null`, it has no description.")]
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
    [Description("The value as it is stored; of an `int` property, the number as text.")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the label shown for the value.
    /// </summary>
    [Description("The label the value is shown with.")]
    public string? Label { get; set; }
}

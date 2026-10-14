using System.ComponentModel;

namespace Simplic.OxS.Server.Controllers.Model;

/// <summary>
/// Response model for an addon definition.
/// </summary>
public class AddonDefinitionResponse
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    [Description("The id of the definition.")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the extendable entity id the key lives under.
    /// </summary>
    [Description("The id of the entity the property belongs to: the entity's current id, also where the definition was stored under an id the entity had before.")]
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage path under the addon bag.
    /// </summary>
    [Description("The key of the property under the entity's `addon` member.")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    [Description("The kind the property is filtered and sorted as: `string`, `int`, `long`, `double`, `decimal`, `bool`, `date`, `dateTime`, `guid` or `object`.")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the closed value list, when the key has one.
    /// </summary>
    [Description("The values the property takes, where it takes no other; absent or `null` otherwise.")]
    public List<AddonDefinitionValueResponse>? Values { get; set; }

    /// <summary>
    /// Gets or sets the human label.
    /// </summary>
    [Description("The label the property is shown with.")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    [Description("What the property means.")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether the definition is retired.
    /// </summary>
    [Description("Whether the definition is retired: it is stored, and its key is no property of the entity any more.")]
    public bool Retired { get; set; }
}

/// <summary>
/// One entry of a closed value list.
/// </summary>
public class AddonDefinitionValueResponse
{
    /// <summary>
    /// Gets or sets the value, as the bag stores it.
    /// </summary>
    [Description("The value as it is stored.")]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the label shown for the value.
    /// </summary>
    [Description("The label the value is shown with.")]
    public string? Label { get; set; }
}

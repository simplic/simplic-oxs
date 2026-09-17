namespace Simplic.OxS.Server.Controllers.Model;

/// <summary>
/// Response model for an addon definition.
/// </summary>
public class AddonDefinitionResponse
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the extendable entity id the key lives under.
    /// </summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage path under the addon bag.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the closed value list, when the key has one.
    /// </summary>
    public List<AddonDefinitionValueResponse>? Values { get; set; }

    /// <summary>
    /// Gets or sets the human label.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether the definition is retired.
    /// </summary>
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
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the label shown for the value.
    /// </summary>
    public string? Label { get; set; }
}

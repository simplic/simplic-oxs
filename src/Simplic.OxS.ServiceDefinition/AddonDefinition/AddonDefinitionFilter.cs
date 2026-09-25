using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Filter for <see cref="AddonDefinitionDocument"/> queries.
/// </summary>
public class AddonDefinitionFilter : OrganizationFilterBase
{
    /// <summary>
    /// Gets or sets the entity id to filter by (e.g. "logistics.shipment").
    /// </summary>
    public string? Entity { get; set; }

    /// <summary>
    /// Gets or sets the path to filter by.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets whether to filter retired definitions in (true), out (false) or not at all (null).
    /// </summary>
    public bool? Retired { get; set; }
}

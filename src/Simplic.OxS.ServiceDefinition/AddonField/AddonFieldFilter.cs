using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Filter for <see cref="AddonField"/> queries.
/// </summary>
public class AddonFieldFilter : OrganizationFilterBase
{
    /// <summary>
    /// Gets or sets the object name to filter by (e.g. "logistics.shipment").
    /// </summary>
    public string? ObjectName { get; set; }

    /// <summary>
    /// Gets or sets the property name to filter by.
    /// </summary>
    public string? PropertyName { get; set; }
}

using Simplic.OxS.Data;

namespace Simplic.OxS.ServiceDefinition;

/// <summary>
/// Represents a single addon field configuration for a given object type within an organization.
/// </summary>
public class AddonField : OrganizationDocumentBase
{
    /// <summary>
    /// Gets or sets the name of the object this addon field belongs to (e.g. "logistics.shipment").
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the addon property.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the addon property (e.g. "string", "double", "int", "bool").
    /// </summary>
    public string PropertyType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional human-readable description of the addon field.
    /// </summary>
    public string? Description { get; set; }
}

namespace Simplic.OxS.Server.Controllers.Model;

/// <summary>
/// Request model for creating an addon field.
/// </summary>
public class CreateAddonFieldRequest
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

/// <summary>
/// Request model for updating an addon field.
/// </summary>
public class UpdateAddonFieldRequest
{
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

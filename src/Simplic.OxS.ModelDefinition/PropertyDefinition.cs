namespace Simplic.OxS.ModelDefinition;

/// <summary>
/// Represents the definition of a property.
/// </summary>
public class PropertyDefinition
{
    /// <summary>
    /// Gets or sets the name of the property.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type of the property.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the description of the property.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whethter the property is internal, or readonly.
    /// </summary>
    public bool Internal { get; set; }

    /// <summary>
    /// Gets or sets whether the property is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets whether the property is nullable.
    /// </summary>
    public bool Nullable { get; set; }

    /// <summary>
    /// Gets or sets the enum type, in case of an enum property.
    /// </summary>
    public string? EnumType { get; set; }

    /// <summary>
    /// Gets or sets items of en enum type.
    /// </summary>
    public IList<EnumItem>? EnumItems { get; set; }

    /// <summary>
    /// Gets or sets the format.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets the minimum value.
    /// </summary>
    public string? MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value.
    /// </summary>
    public string? MaxValue { get; set; }

    /// <summary>
    /// Gets or sets the type of an array.
    /// </summary>
    public string? ArrayType { get; set; }

    /// <summary>
    /// Gets or sets whether the array requires an id and is patchable per object (true) or the array is 
    /// taken as it is (false).
    /// </summary>
    public bool? PatchableArray { get; set; }

    /// <summary>
    /// Gets or sets in case of a reference type which 
    /// </summary>
    public string? ReferenceId { get; set; }

    public IList<string>? AvailableTypes { get; set; }
}

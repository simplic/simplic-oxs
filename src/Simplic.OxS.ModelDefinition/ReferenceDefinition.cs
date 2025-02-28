namespace Simplic.OxS.ModelDefinition;

/// <summary>
/// Represents the definition of an referenced object or another request.
/// </summary>
public class ReferenceDefinition
{
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// Gets or sets the source url.
    /// </summary>
    public string SourceUrl { get; set; }

    /// <summary>
    /// Gets or sets the name of the property that is used to reference the object.
    /// </summary>
    public string ReferencePropertyName { get; set; }

    /// <summary>
    /// Gets or sets the operation.
    /// </summary>
    public OperationDefinition Operation { get; set; }

    /// <summary>
    /// Gets or sets the properties of the reference.
    /// </summary>
    public IList<PropertyDefinition> Properties { get; set; }

    /// <summary>
    /// Gets or sets the optional search key. (prefix for the search api)
    /// </summary>
    public string? SearchKey { get; set; }
}
namespace Simplic.OxS.ModelDefinition;

/// <summary>
/// A model definition is a summary of the content contained within a controller. 
/// A collection of all request and response objects, their properties and data sources.
/// </summary>
public class ModelDefinition
{
    /// <summary>
    /// Gets or sets the title of the model definition.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or setss the source url.
    /// </summary>
    public string SourceUrl { get; set; }

    public Operations Operations { get; set; }
        = new Operations();

    public IList<DataSource> DataSources { get; set; }
        = new List<DataSource>();

    public IList<PropertyDefinition> Properties { get; set; }
        = new List<PropertyDefinition>();

    public IList<ReferenceDefinition> References { get; set; }
        = new List<ReferenceDefinition>();
}

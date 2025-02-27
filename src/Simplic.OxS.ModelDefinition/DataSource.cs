namespace Simplic.OxS.ModelDefinition;

/// <summary>
/// Represents a data source for a model. E.g. an rest response and its endpoint.
/// </summary>
public class DataSource
{
    /// <summary>
    /// Gets or sets the type of the data source.
    /// </summary>
    public DataSourceType Type { get; set; }

    /// <summary>
    /// Gets or sets the endpoint.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the graphQl entry point.
    /// </summary>
    public string? GqlEntryPoint { get; set; }
}

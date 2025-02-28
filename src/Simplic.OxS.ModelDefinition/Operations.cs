namespace Simplic.OxS.ModelDefinition;

/// <summary>
/// Represents all operations for a model definition.
/// </summary>
public class Operations
{
    /// <summary>
    /// Gets or sets the create definition if available.
    /// </summary>
    public OperationDefinition? Create { get; set; }

    /// <summary>
    /// Gets or sets the update definition if available.
    /// </summary>
    public OperationDefinition? Update { get; set; }

    /// <summary>
    /// Gets or sets the delete definition if available.
    /// </summary>
    public OperationDefinition? Delete { get; set; }

    /// <summary>
    /// Gets or sets the get definition if available.
    /// </summary>
    public OperationDefinition? Get { get; set; }
}

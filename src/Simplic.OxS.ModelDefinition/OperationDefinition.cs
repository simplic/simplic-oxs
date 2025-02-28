namespace Simplic.OxS.ModelDefinition;

/// <summary>
/// Represents the definition of an operation for the model definition.
/// </summary>
public class OperationDefinition
{
    /// <summary>
    /// Gets or sets the operation type, e.g "http-get", "http-delete" etc.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the endpoint for the operation.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the request reference/ referenced object.
    /// </summary>
    public string? RequestReference { get; set; }

    /// <summary>
    /// Gets or setss the response reference/ referenced object.
    /// </summary>
    public string? ResponseReference { get; set; }
}

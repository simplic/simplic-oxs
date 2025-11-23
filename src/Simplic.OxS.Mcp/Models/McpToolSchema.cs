using System.Text.Json.Serialization;

namespace Simplic.OxS.Mcp;

/// <summary>
/// Represents a JSON schema for MCP tool parameters
/// </summary>
public class McpToolSchema
{
    /// <summary>
    /// Gets or sets the type of the schema (usually "object")
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; } = "object";

    /// <summary>
    /// Gets or sets the properties of the schema
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, McpSchemaProperty>? Properties { get; set; }

    /// <summary>
    /// Gets or sets the required properties
    /// </summary>
    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }

    /// <summary>
    /// Gets or sets whether additional properties are allowed
    /// </summary>
    [JsonPropertyName("additionalProperties")]
    public bool AdditionalProperties { get; set; } = false;
}

/// <summary>
/// Represents a property in an MCP tool schema
/// </summary>
public class McpSchemaProperty
{
    /// <summary>
    /// Gets or sets the type of the property
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the description of the property
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the default value
    /// </summary>
    [JsonPropertyName("default")]
    public object? Default { get; set; }

    /// <summary>
    /// Gets or sets enum values for string types
    /// </summary>
    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }

    /// <summary>
    /// Gets or sets the format for string types
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets minimum value for numeric types
    /// </summary>
    [JsonPropertyName("minimum")]
    public double? Minimum { get; set; }

    /// <summary>
    /// Gets or sets maximum value for numeric types
    /// </summary>
    [JsonPropertyName("maximum")]
    public double? Maximum { get; set; }
}
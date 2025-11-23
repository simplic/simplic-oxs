using System.Text.Json.Serialization;

namespace Simplic.OxS.Mcp;

/// <summary>
/// Represents an MCP tool that can be invoked by clients
/// </summary>
public class McpTool
{
    /// <summary>
    /// Gets or sets the unique identifier for the tool
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of what the tool does
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema for the input parameters
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public required McpToolSchema InputSchema { get; set; }
}
using System.Text.Json.Serialization;

namespace Simplic.OxS.Mcp;

/// <summary>
/// Represents a request to execute an MCP tool
/// </summary>
public class McpToolRequest
{
    /// <summary>
    /// Gets or sets the method name (should be "tools/call")
    /// </summary>
    [JsonPropertyName("method")]
    public required string Method { get; set; }

    /// <summary>
    /// Gets or sets the parameters for the request
    /// </summary>
    [JsonPropertyName("params")]
    public required McpToolRequestParams Params { get; set; }
}

/// <summary>
/// Represents the parameters for an MCP tool request
/// </summary>
public class McpToolRequestParams
{
    /// <summary>
    /// Gets or sets the name of the tool to execute
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the arguments to pass to the tool
    /// </summary>
    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }
}

/// <summary>
/// Represents the response from an MCP tool execution
/// </summary>
public class McpToolResponse
{
    /// <summary>
    /// Gets or sets the result of the tool execution
    /// </summary>
    [JsonPropertyName("result")]
    public McpToolResult? Result { get; set; }

    /// <summary>
    /// Gets or sets any error that occurred
    /// </summary>
    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

/// <summary>
/// Represents an MCP error
/// </summary>
public class McpError
{
    /// <summary>
    /// Gets or sets the error code
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets additional error data
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
using System.Text.Json.Serialization;

namespace Simplic.OxS.Mcp;

/// <summary>
/// Represents the result of an MCP tool execution
/// </summary>
public class McpToolResult
{
    /// <summary>
    /// Gets or sets whether the tool execution was successful
    /// </summary>
    [JsonPropertyName("isSuccess")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the content returned by the tool
    /// </summary>
    [JsonPropertyName("content")]
    public List<McpContent>? Content { get; set; }

    /// <summary>
    /// Gets or sets any error message if execution failed
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Creates a successful result with content
    /// </summary>
    public static McpToolResult Success(List<McpContent> content) => new()
    {
        IsSuccess = true,
        Content = content
    };

    /// <summary>
    /// Creates a successful result with text content
    /// </summary>
    public static McpToolResult Success(string text) => new()
    {
        IsSuccess = true,
        Content = [new McpContent { Type = "text", Text = text }]
    };

    /// <summary>
    /// Creates a failed result with error message
    /// </summary>
    public static McpToolResult Failed(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };
}

/// <summary>
/// Represents content in an MCP result
/// </summary>
public class McpContent
{
    /// <summary>
    /// Gets or sets the type of content (text, image, etc.)
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the text content (for text type)
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the MIME type (for non-text types)
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets the data (for non-text types, base64 encoded)
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }
}
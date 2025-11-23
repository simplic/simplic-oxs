namespace Simplic.OxS.Mcp;

/// <summary>
/// Interface for implementing MCP tools
/// </summary>
public interface IMcpTool
{
    /// <summary>
    /// Gets the tool definition
    /// </summary>
    /// <returns>Tool definition</returns>
    McpTool GetToolDefinition();

    /// <summary>
    /// Executes the tool with the provided arguments
    /// </summary>
    /// <param name="arguments">Tool arguments</param>
    /// <param name="context">Request context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool execution result</returns>
    Task<McpToolResult> ExecuteAsync(IDictionary<string, object>? arguments
                                   , IRequestContext context
                                   , CancellationToken cancellationToken = default);
}
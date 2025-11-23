namespace Simplic.OxS.Mcp.Server.Services;

/// <summary>
/// Service for executing MCP tools
/// </summary>
public interface IMcpExecutionService
{
    /// <summary>
    /// Executes an MCP tool by name
    /// </summary>
    /// <param name="toolName">Name of the tool to execute</param>
    /// <param name="arguments">Tool arguments</param>
    /// <param name="context">Request context</param>
    /// <param name="cancellationToken">Cancellation token</param>
 /// <returns>Tool execution result</returns>
    Task<McpToolResult> ExecuteToolAsync(
    string toolName,
      Dictionary<string, object>? arguments,
  IRequestContext context,
        CancellationToken cancellationToken = default);
}
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Mcp.Server.Services;

/// <summary>
/// Default implementation of IMcpExecutionService
/// </summary>
public class McpExecutionService : IMcpExecutionService
{
    private readonly IMcpToolRegistry _toolRegistry;
    private readonly ILogger<McpExecutionService> _logger;

    /// <summary>
    /// Initializes a new instance of the McpExecutionService class
  /// </summary>
    public McpExecutionService(IMcpToolRegistry toolRegistry, ILogger<McpExecutionService> logger)
    {
_toolRegistry = toolRegistry;
 _logger = logger;
}

    /// <inheritdoc />
    public async Task<McpToolResult> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object>? arguments,
   IRequestContext context,
        CancellationToken cancellationToken = default)
    {
        try
      {
            var tool = _toolRegistry.GetTool(toolName);
            if (tool == null)
      {
        _logger.LogWarning("Tool {ToolName} not found", toolName);
                return McpToolResult.Failed($"Tool '{toolName}' not found");
 }

      _logger.LogInformation(
                "Executing MCP tool {ToolName} for user {UserId} in organization {OrganizationId}",
       toolName,
  context.UserId,
           context.OrganizationId);

      var result = await tool.ExecuteAsync(arguments, context, cancellationToken);

          _logger.LogInformation(
    "MCP tool {ToolName} execution completed successfully: {IsSuccess}",
         toolName,
       result.IsSuccess);

    return result;
        }
        catch (Exception ex)
        {
        _logger.LogError(ex, "Error executing MCP tool {ToolName}", toolName);
       return McpToolResult.Failed($"Internal error: {ex.Message}");
        }
    }
}
namespace Simplic.OxS.Mcp;

/// <summary>
/// Registry for managing MCP tools
/// </summary>
public interface IMcpToolRegistry
{
    /// <summary>
    /// Registers an MCP tool
    /// </summary>
    /// <param name="tool">Tool to register</param>
    void RegisterTool(IMcpTool tool);

    /// <summary>
    /// Gets all registered tools
    /// </summary>
    /// <returns>List of registered tools</returns>
    IEnumerable<IMcpTool> GetTools();

    /// <summary>
    /// Gets a specific tool by name
    /// </summary>
    /// <param name="name">Tool name</param>
    /// <returns>Tool if found, null otherwise</returns>
    IMcpTool? GetTool(string name);

    /// <summary>
    /// Gets all tool definitions
    /// </summary>
    /// <returns>List of tool definitions</returns>
    IEnumerable<McpTool> GetToolDefinitions();
}
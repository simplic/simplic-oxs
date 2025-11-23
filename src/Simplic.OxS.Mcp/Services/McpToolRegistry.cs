using Microsoft.Extensions.DependencyInjection;

namespace Simplic.OxS.Mcp;

/// <summary>
/// Default implementation of IMcpToolRegistry
/// </summary>
public class McpToolRegistry : IMcpToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _tools = new();
    private readonly IServiceProvider _serviceProvider;

    public McpToolRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public void RegisterTool(IMcpTool tool)
    {
        var definition = tool.GetToolDefinition();
        _tools[definition.Name] = tool;
    }

    /// <inheritdoc />
    public IEnumerable<IMcpTool> GetTools()
    {
        return _tools.Values;
    }

    /// <inheritdoc />
    public IMcpTool? GetTool(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    /// <inheritdoc />
    public IEnumerable<McpTool> GetToolDefinitions()
    {
        return _tools.Values.Select(tool => tool.GetToolDefinition());
    }

    /// <summary>
    /// Registers all tools found in the dependency injection container
    /// </summary>
    public void RegisterDiscoveredTools()
    {
        var tools = _serviceProvider.GetServices<IMcpTool>();
        foreach (var tool in tools)
        {
            RegisterTool(tool);
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Mcp.Server.Services;

namespace Simplic.OxS.Mcp.Server.Extensions;

/// <summary>
/// Extension methods for configuring MCP server in DI container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCP server services to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddMcpServer(this IServiceCollection services)
    {
    // Register core MCP services
        services.AddSingleton<IMcpToolRegistry, McpToolRegistry>();
    services.AddScoped<IMcpExecutionService, McpExecutionService>();

      return services;
    }

 /// <summary>
    /// Adds an MCP tool to the DI container
    /// </summary>
    /// <typeparam name="T">Tool type</typeparam>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddMcpTool<T>(this IServiceCollection services)
        where T : class, IMcpTool
    {
 services.AddScoped<IMcpTool, T>();
return services;
    }

    /// <summary>
    /// Adds multiple MCP tools to the DI container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="toolTypes">Types of tools to add</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddMcpTools(this IServiceCollection services, params Type[] toolTypes)
    {
   foreach (var toolType in toolTypes)
        {
            if (!typeof(IMcpTool).IsAssignableFrom(toolType))
      {
    throw new ArgumentException($"Type {toolType.Name} does not implement IMcpTool");
   }

   services.AddScoped(typeof(IMcpTool), toolType);
  }

  return services;
    }
}
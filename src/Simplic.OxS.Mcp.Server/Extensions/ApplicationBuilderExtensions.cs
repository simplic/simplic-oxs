using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Simplic.OxS.Mcp.Server.Extensions;

/// <summary>
/// Extension methods for configuring MCP server in the application pipeline
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Configures MCP server in the application pipeline
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder</returns>
    public static IApplicationBuilder UseMcpServer(this IApplicationBuilder app)
    {
     // Register discovered MCP tools
        using var scope = app.ApplicationServices.CreateScope();
     var toolRegistry = scope.ServiceProvider.GetRequiredService<IMcpToolRegistry>();
    
      if (toolRegistry is McpToolRegistry registry)
 {
      registry.RegisterDiscoveredTools();
   }

     return app;
 }
}
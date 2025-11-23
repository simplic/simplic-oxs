using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Mcp.Server.Services;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Extensions;
using System.Text.Json;

namespace Simplic.OxS.Mcp.Server.Controllers;

/// <summary>
/// Controller for handling MCP server endpoints
/// </summary>
[ApiController]
[Route("mcp")]
[Authorize(Policy = "JwtOnly")] // Only allow JWT authentication for MCP
public class McpServerController : OxSController
{
    private readonly IMcpToolRegistry _toolRegistry;
    private readonly IMcpExecutionService _executionService;
    private readonly ILogger<McpServerController> _logger;

    /// <summary>
    /// Initializes a new instance of the McpServerController class
    /// </summary>
    public McpServerController(
                IMcpToolRegistry toolRegistry,
                IMcpExecutionService executionService,
                ILogger<McpServerController> logger)
    {
        _toolRegistry = toolRegistry;
        _executionService = executionService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the server capabilities and information
    /// </summary>
    [HttpGet("capabilities")]
    public IActionResult GetCapabilities()
    {
        var capabilities = new
        {
            name = "Simplic.OxS MCP Server",
            version = "1.0.0",
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { },
                logging = new { }
            }
        };

        return Ok(capabilities);
    }

    /// <summary>
    /// Lists all available tools
    /// </summary>
    [HttpPost("tools/list")]
    public IActionResult ListTools()
    {
        try
        {
            var tools = _toolRegistry.GetToolDefinitions();
            var result = new
            {
                tools = tools
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list MCP tools");
            return StatusCode(500, new McpError
            {
                Code = -32603,
                Message = "Internal server error while listing tools"
            });
        }
    }

    /// <summary>
    /// Executes an MCP tool
    /// </summary>
    [HttpPost("tools/call")]
    public async Task<IActionResult> CallTool([FromBody] McpToolRequest request)
    {
        try
        {
            if (request.Method != "tools/call")
            {
                return BadRequest(new McpError
                {
                    Code = -32600,
                    Message = "Invalid request method. Expected 'tools/call'"
                });
            }

            var requestContext = HttpContext.RequestServices.GetRequiredService<IRequestContext>();
            var result = await _executionService.ExecuteToolAsync(
                           request.Params.Name,
                    request.Params.Arguments,
                requestContext,
                   HttpContext.RequestAborted);

            if (!result.IsSuccess)
            {
                return StatusCode(500, new McpToolResponse
                {
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = result.Error ?? "Tool execution failed"
                    }
                });
            }

            return Ok(new McpToolResponse
            {
                Result = result
            });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in tool call request");
            return BadRequest(new McpError
            {
                Code = -32700,
                Message = "Parse error: Invalid JSON"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute MCP tool {ToolName}", request.Params?.Name);
            return StatusCode(500, new McpError
            {
                Code = -32603,
                Message = "Internal server error during tool execution"
            });
        }
    }

    /// <summary>
    /// Health check endpoint for MCP server
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            tools = _toolRegistry.GetTools().Count()
        });
    }
}
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Simplic.OxS.Mcp.Tests;

/// <summary>
/// Integration tests for MCP functionality
/// Demonstrates how to test MCP tools and endpoints
/// </summary>
public class McpIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public McpIntegrationTests(WebApplicationFactory<Program> factory)
    {
    _factory = factory;
        _client = factory.CreateClient();
        
        // Set up JWT authentication (in real tests, generate proper JWT tokens)
        var token = GenerateTestJwtToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task GetCapabilities_ReturnsServerInfo()
    {
  // Act
        var response = await _client.GetAsync("/mcpsample-api/v1/mcp/capabilities");
        
      // Assert
  response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var capabilities = JsonDocument.Parse(content);
        
        Assert.Equal("Simplic.OxS MCP Server", capabilities.RootElement.GetProperty("name").GetString());
 Assert.Equal("1.0.0", capabilities.RootElement.GetProperty("version").GetString());
    }

    [Fact]
 public async Task ListTools_ReturnsAvailableTools()
    {
   // Act
        var response = await _client.PostAsync("/mcpsample-api/v1/mcp/tools/list", 
   new StringContent("{}", Encoding.UTF8, "application/json"));
        
    // Assert
        response.EnsureSuccessStatusCode();
      var content = await response.Content.ReadAsStringAsync();
  var result = JsonDocument.Parse(content);
        
   var tools = result.RootElement.GetProperty("tools").EnumerateArray();
  Assert.True(tools.Any(t => t.GetProperty("name").GetString() == "calculate"));
      Assert.True(tools.Any(t => t.GetProperty("name").GetString() == "get_organization_info"));
    }

    [Fact]
    public async Task CallTool_Calculator_ReturnsCorrectResult()
    {
        // Arrange
        var request = new
        {
            method = "tools/call",
            @params = new
   {
    name = "calculate",
         arguments = new
        {
      operation = "add",
     operand1 = 10,
         operand2 = 5
      }
       }
      };

      var json = JsonSerializer.Serialize(request);
        
        // Act
        var response = await _client.PostAsync("/mcpsample-api/v1/mcp/tools/call",
      new StringContent(json, Encoding.UTF8, "application/json"));
        
        // Assert
   response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(content);
        
        Assert.True(result.RootElement.GetProperty("result").GetProperty("isSuccess").GetBoolean());
  
     var resultContent = result.RootElement.GetProperty("result").GetProperty("content").EnumerateArray().First();
        var text = resultContent.GetProperty("text").GetString();
    Assert.Contains("15", text); // Should contain the result
    }

    [Fact]
    public async Task CallTool_OrganizationInfo_ReturnsUserContext()
    {
        // Arrange
        var request = new
        {
     method = "tools/call",
            @params = new
            {
 name = "get_organization_info",
        arguments = new
         {
        includeDetails = true
                }
     }
        };

    var json = JsonSerializer.Serialize(request);
        
        // Act
        var response = await _client.PostAsync("/mcpsample-api/v1/mcp/tools/call",
 new StringContent(json, Encoding.UTF8, "application/json"));
     
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonDocument.Parse(content);
     
        Assert.True(result.RootElement.GetProperty("result").GetProperty("isSuccess").GetBoolean());
  }

    [Fact]
    public async Task CallTool_InvalidTool_ReturnsError()
    {
        // Arrange
   var request = new
  {
     method = "tools/call",
        @params = new
            {
                name = "nonexistent_tool",
       arguments = new { }
        }
        };

        var json = JsonSerializer.Serialize(request);
        
        // Act
        var response = await _client.PostAsync("/mcpsample-api/v1/mcp/tools/call",
       new StringContent(json, Encoding.UTF8, "application/json"));
    
      // Assert
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
    }

 [Fact]
    public async Task GetHealth_ReturnsHealthStatus()
    {
      // Act
    var response = await _client.GetAsync("/mcpsample-api/v1/mcp/health");

        // Assert
        response.EnsureSuccessStatusCode();
     var content = await response.Content.ReadAsStringAsync();
 var health = JsonDocument.Parse(content);
        
        Assert.Equal("healthy", health.RootElement.GetProperty("status").GetString());
        Assert.True(health.RootElement.GetProperty("tools").GetInt32() >= 0);
    }

    /// <summary>
    /// Unit test for individual tool execution
    /// </summary>
    [Fact]
    public async Task TestCalculatorTool_Directly()
  {
        // Arrange
        var tool = new CalculatorTool();
 var mockContext = new TestRequestContext 
        { 
UserId = Guid.NewGuid(), 
     OrganizationId = Guid.NewGuid() 
     };
      
        var arguments = new Dictionary<string, object>
    {
       ["operation"] = "multiply",
   ["operand1"] = 6,
        ["operand2"] = 7
    };

 // Act
        var result = await tool.ExecuteAsync(arguments, mockContext);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Content);
        Assert.Single(result.Content);
Assert.Contains("42", result.Content[0].Text);
    }

    /// <summary>
    /// Test tool schema generation
    /// </summary>
    [Fact]
    public void TestToolDefinition_Calculator()
    {
        // Arrange
        var tool = new CalculatorTool();
        
        // Act
        var definition = tool.GetToolDefinition();
      
   // Assert
        Assert.Equal("calculate", definition.Name);
        Assert.NotNull(definition.Description);
        Assert.NotNull(definition.InputSchema);
        Assert.Equal("object", definition.InputSchema.Type);
        
     // Check required properties
    Assert.Contains("operation", definition.InputSchema.Properties.Keys);
      Assert.Contains("operand1", definition.InputSchema.Properties.Keys);
  Assert.Contains("operand2", definition.InputSchema.Properties.Keys);
        
  // Verify required fields
     Assert.Contains("operation", definition.InputSchema.Required);
        Assert.Contains("operand1", definition.InputSchema.Required);
        Assert.Contains("operand2", definition.InputSchema.Required);
    }

    /// <summary>
    /// Generates a test JWT token with required claims
    /// In real tests, use proper JWT generation libraries
    /// </summary>
    private string GenerateTestJwtToken()
{
        // This is a mock token - in real tests, generate proper JWTs
     // with valid signatures and the required claims:
        // - Id (User ID)
        // - OId (Organization ID)
        
     return "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJJZCI6IjEyM2U0NTY3LWU4OWItMTJkMy1hNDU2LTQyNjYxNDE3NDAwMCIsIk9JZCI6Ijk4NzY1NDMyLWUxMmYtMzRhNS1iNjc4LTEyMzQ1Njc4OTAxMiIsImV4cCI6OTk5OTk5OTk5OX0.placeholder";
    }
}

/// <summary>
/// Test implementation of IRequestContext
/// </summary>
public class TestRequestContext : IRequestContext
{
    public Guid? CorrelationId { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
}

/// <summary>
/// Example of custom MCP tool for testing
/// </summary>
public class TestCustomTool : McpToolBase
{
    [McpTool("test_tool", "A tool for testing purposes")]
    public async Task<McpToolResult> TestOperation(
        string input,
 IRequestContext context = null!,
        CancellationToken cancellationToken = default)
    {
     await Task.Delay(1); // Simulate async operation
        return McpToolResult.Success($"Processed: {input} for user {context.UserId}");
    }

    public override async Task<McpToolResult> ExecuteAsync(
        Dictionary<string, object>? arguments,
        IRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var input = arguments?["input"]?.ToString() ?? "";
        return await TestOperation(input, context, cancellationToken);
    }
}
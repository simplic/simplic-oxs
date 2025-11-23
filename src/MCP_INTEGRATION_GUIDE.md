# Adding MCP (Model Context Protocol) Support to Simplic.OxS Microservices

## Overview

The MCP system has been added to the Simplic.OxS framework as an optional extension. It allows microservices to expose AI tools and capabilities via HTTP/HTTPS with JWT authentication, following the Model Context Protocol specification.

## Architecture

The MCP system consists of three main components:

### 1. Simplic.OxS.Mcp (Core Library)
- **Purpose**: Core abstractions, models, and interfaces for MCP tools
- **Key Types**: `IMcpTool`, `McpTool`, `McpToolResult`, `McpToolSchema`
- **Dependencies**: Only core .NET libraries and `Simplic.OxS`

### 2. Simplic.OxS.Mcp.Server (HTTP Server)
- **Purpose**: ASP.NET Core controllers and services for MCP HTTP endpoints
- **Key Features**: JWT authentication, tool execution, error handling
- **Dependencies**: ASP.NET Core, `Simplic.OxS.Mcp`, `Simplic.OxS.Server`

### 3. Integration with Bootstrap
- **Purpose**: Seamless integration with existing Simplic.OxS microservices
- **Key Features**: Optional loading, runtime discovery, no breaking changes

## Integration Steps

### Step 1: Add Project References

Add the MCP projects to your solution and reference them in your service:

```xml
<ItemGroup>
  <!-- Your existing references -->
  <ProjectReference Include="..\Simplic.OxS.Mcp\Simplic.OxS.Mcp.csproj" />
  <ProjectReference Include="..\Simplic.OxS.Mcp.Server\Simplic.OxS.Mcp.Server.csproj" />
</ItemGroup>
```

### Step 2: Enable MCP in Your Service

In your service's `Startup` class that inherits from `Bootstrap`, override the `IsMcpEnabled` method:

```csharp
public class MyServiceStartup : Bootstrap
{
    public MyServiceStartup(IConfiguration configuration, IWebHostEnvironment currentEnvironment) 
        : base(configuration, currentEnvironment)
    {
    }

// Enable MCP functionality
    protected override bool IsMcpEnabled() => true;

    // Register your custom MCP tools
 protected override void ConfigureMcpServices(IServiceCollection services)
    {
        services.AddMcpTool<MyCustomTool>();
        services.AddMcpTool<AnotherTool>();
        
// Or register multiple tools at once:
   // services.AddMcpTools(typeof(MyCustomTool), typeof(AnotherTool));
    }

    // Your existing service registration
    protected override void RegisterServices(IServiceCollection services)
    {
      // Your existing service registrations
    }

    protected override string ServiceName => "MyService";
}
```

### Step 3: Create MCP Tools

#### Option A: Using McpToolBase (Recommended)

```csharp
using Simplic.OxS.Mcp;
using System.ComponentModel;

public class UserManagementTool : McpToolBase
{
    private readonly IUserService _userService;

    public UserManagementTool(IUserService userService)
    {
        _userService = userService;
    }

    [McpTool("get_user_info", "Retrieves information about a user")]
    public async Task<McpToolResult> GetUserInfo(
        [Description("The ID of the user to retrieve")]
        Guid userId,
 [Description("Whether to include detailed information")]
     bool includeDetails = false,
    IRequestContext context = null!,
 CancellationToken cancellationToken = default)
    {
      try
        {
   // Verify permissions using context
         if (context.UserId != userId && !await _userService.CanAccessUser(context.UserId, userId))
       {
              return McpToolResult.Failed("Access denied");
            }

            var user = await _userService.GetUserAsync(userId, cancellationToken);
   if (user == null)
  {
         return McpToolResult.Failed("User not found");
  }

   var result = includeDetails ? 
       JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true }) :
         $"User: {user.Name} ({user.Email})";

            return McpToolResult.Success(result);
    }
        catch (Exception ex)
        {
            return McpToolResult.Failed($"Error retrieving user: {ex.Message}");
        }
    }

    public override async Task<McpToolResult> ExecuteAsync(
        Dictionary<string, object>? arguments,
IRequestContext context,
   CancellationToken cancellationToken = default)
    {
        if (!arguments?.TryGetValue("userId", out var userIdObj) == true)
       return McpToolResult.Failed("userId parameter is required");

      if (!Guid.TryParse(userIdObj?.ToString(), out var userId))
      return McpToolResult.Failed("Invalid userId format");

        var includeDetails = arguments.TryGetValue("includeDetails", out var detailsObj) && 
   bool.TryParse(detailsObj?.ToString(), out var details) && details;

    return await GetUserInfo(userId, includeDetails, context, cancellationToken);
    }
}
```

#### Option B: Implementing IMcpTool Directly

```csharp
public class CustomReportTool : IMcpTool
{
  private readonly IReportService _reportService;

    public CustomReportTool(IReportService reportService)
    {
        _reportService = reportService;
    }

    public McpTool GetToolDefinition()
    {
        return new McpTool
        {
            Name = "generate_report",
  Description = "Generates a custom report",
 InputSchema = new McpToolSchema
     {
    Type = "object",
                Properties = new Dictionary<string, McpSchemaProperty>
   {
      ["reportType"] = new McpSchemaProperty 
       { 
   Type = "string", 
          Description = "Type of report to generate",
  Enum = new List<string> { "sales", "inventory", "users" }
     },
    ["startDate"] = new McpSchemaProperty 
  { 
     Type = "string", 
   Format = "date",
      Description = "Start date for the report" 
        },
         ["endDate"] = new McpSchemaProperty 
            { 
            Type = "string", 
      Format = "date",
Description = "End date for the report" 
    }
      },
       Required = new List<string> { "reportType", "startDate", "endDate" }
            }
        };
    }

    public async Task<McpToolResult> ExecuteAsync(
        Dictionary<string, object>? arguments,
   IRequestContext context,
        CancellationToken cancellationToken = default)
    {
      try
 {
    if (arguments == null)
     return McpToolResult.Failed("Arguments are required");

   var reportType = arguments["reportType"]?.ToString();
    var startDate = DateTime.Parse(arguments["startDate"]?.ToString() ?? "");
  var endDate = DateTime.Parse(arguments["endDate"]?.ToString() ?? "");

     var reportData = await _reportService.GenerateReportAsync(
      reportType, startDate, endDate, context.OrganizationId, cancellationToken);

     return McpToolResult.Success(JsonSerializer.Serialize(reportData, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
     {
            return McpToolResult.Failed($"Report generation failed: {ex.Message}");
        }
    }
}
```

## Available MCP Endpoints

When MCP is enabled, your service will automatically expose these endpoints:

### 1. Server Capabilities
```http
GET /{service-name}-api/v1/mcp/capabilities
Authorization: Bearer {jwt-token}
```

### 2. List Tools
```http
POST /{service-name}-api/v1/mcp/tools/list
Authorization: Bearer {jwt-token}
Content-Type: application/json

{}
```

### 3. Execute Tool
```http
POST /{service-name}-api/v1/mcp/tools/call
Authorization: Bearer {jwt-token}
Content-Type: application/json

{
    "method": "tools/call",
    "params": {
        "name": "get_user_info",
      "arguments": {
          "userId": "123e4567-e89b-12d3-a456-426614174000",
            "includeDetails": true
        }
    }
}
```

### 4. Health Check
```http
GET /{service-name}-api/v1/mcp/health
Authorization: Bearer {jwt-token}
```

## Security Considerations

1. **JWT Authentication**: All MCP endpoints require valid JWT tokens with user and organization claims
2. **Permission Checking**: Tools should validate permissions using the provided `IRequestContext`
3. **Input Validation**: Always validate and sanitize tool arguments
4. **Error Handling**: Never expose sensitive information in error messages

## Best Practices

### Tool Design
- Use descriptive, consistent naming (lowercase with underscores)
- Provide clear descriptions for tools and parameters
- Design for composability - tools should do one thing well
- Return structured data when possible

### Performance
- Use async/await for I/O operations
- Consider caching for expensive operations
- Implement proper cancellation token handling
- Monitor tool execution times

### Error Handling
```csharp
try
{
    // Tool implementation
    return McpToolResult.Success(result);
}
catch (ValidationException ex)
{
    return McpToolResult.Failed($"Validation error: {ex.Message}");
}
catch (UnauthorizedAccessException)
{
    return McpToolResult.Failed("Access denied");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Tool execution failed");
    return McpToolResult.Failed("Internal server error");
}
```

### Testing Tools
Tools can be tested independently:

```csharp
[Test]
public async Task TestUserInfoTool()
{
    // Arrange
    var mockUserService = new Mock<IUserService>();
    var tool = new UserManagementTool(mockUserService.Object);
    var context = new Mock<IRequestContext>();
    
    // Act
    var result = await tool.ExecuteAsync(
        new Dictionary<string, object> { ["userId"] = testUserId },
        context.Object);
    
    // Assert
    Assert.IsTrue(result.IsSuccess);
}
```

## Migration from Existing APIs

If you have existing REST APIs that you want to expose as MCP tools:

1. Create wrapper tools that call your existing services
2. Map REST parameters to MCP tool arguments
3. Convert responses to MCP format
4. Maintain backward compatibility

Example:
```csharp
public class ExistingApiWrapperTool : McpToolBase
{
    private readonly IExistingApiService _apiService;

    [McpTool("legacy_operation", "Wraps existing API operation")]
    public async Task<McpToolResult> WrapLegacyOperation(
    string parameter1,
        int parameter2,
      IRequestContext context = null!,
        CancellationToken cancellationToken = default)
{
        // Call existing service
        var result = await _apiService.ExistingMethodAsync(parameter1, parameter2);
    
        // Convert to MCP format
        return McpToolResult.Success(JsonSerializer.Serialize(result));
    }

 // Implementation details...
}
```

## Deployment Notes

- MCP functionality is completely optional - services without MCP references will continue to work normally
- The Bootstrap class uses runtime type loading to avoid compile-time dependencies
- MCP endpoints are automatically documented in Swagger if enabled
- Consider API versioning for MCP tools as they evolve

## Troubleshooting

### Common Issues

1. **"MCP Server not available" message**: 
   - Ensure MCP project references are added
   - Verify the assemblies can be loaded at runtime

2. **Tool not found errors**:
   - Check that tools are properly registered in `ConfigureMcpServices`
   - Verify tool names match exactly

3. **Authentication failures**:
 - Ensure JWT tokens contain required claims (UserId, OrganizationId)
   - Verify the "JwtOnly" policy is working correctly

4. **Schema validation errors**:
   - Check parameter types match the schema
   - Verify required parameters are provided

This implementation provides a robust, secure, and extensible way to add MCP functionality to your Simplic.OxS microservices while maintaining full backward compatibility.
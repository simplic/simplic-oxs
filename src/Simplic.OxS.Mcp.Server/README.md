# Simplic.OxS MCP (Model Context Protocol) Server

The Simplic.OxS MCP Server provides a standardized way for microservices to expose AI tools and capabilities via HTTP/HTTPS with JWT authentication.

## Overview

The MCP Server system consists of:

- **Simplic.OxS.Mcp**: Core abstractions and models for MCP tools
- **Simplic.OxS.Mcp.Server**: HTTP server implementation with JWT authentication
- Integration with the existing Bootstrap system

## Quick Start

### 1. Enable MCP in Your Service

In your service's `Startup` class, override the `IsMcpEnabled` method:

```csharp
public class MyServiceStartup : Bootstrap
{
    // ... existing code ...

    protected override bool IsMcpEnabled() => true;
}
```

### 2. Create MCP Tools

#### Option A: Using McpToolBase

```csharp
public class MyCalculatorTool : McpToolBase
{
    [McpTool("calculate", "Performs basic mathematical calculations")]
    public async Task<McpToolResult> Calculate(
        [Description("The mathematical operation to perform")]
string operation,
        [Description("First number")]
        double operand1,
        [Description("Second number")] 
  double operand2,
        IRequestContext context = null!,
        CancellationToken cancellationToken = default)
    {
        // Your implementation here
var result = operation switch
        {
      "add" => operand1 + operand2,
          "multiply" => operand1 * operand2,
            _ => throw new ArgumentException("Unknown operation")
   };
        
        return McpToolResult.Success($"Result: {result}");
    }

 public override async Task<McpToolResult> ExecuteAsync(
  Dictionary<string, object>? arguments,
   IRequestContext context,
        CancellationToken cancellationToken = default)
    {
        // Extract arguments and call your method
        var operation = arguments?["operation"]?.ToString() ?? "";
        var operand1 = Convert.ToDouble(arguments?["operand1"]);
        var operand2 = Convert.ToDouble(arguments?["operand2"]);
        
        return await Calculate(operation, operand1, operand2, context, cancellationToken);
    }
}
```

#### Option B: Implementing IMcpTool Directly

```csharp
public class CustomTool : IMcpTool
{
    public McpTool GetToolDefinition()
    {
      return new McpTool
        {
    Name = "custom_tool",
            Description = "A custom tool implementation",
       InputSchema = new McpToolSchema
{
                Type = "object",
     Properties = new Dictionary<string, McpSchemaProperty>
         {
            ["input"] = new McpSchemaProperty 
          { 
     Type = "string", 
        Description = "Input text" 
          }
      },
         Required = ["input"]
            }
        };
    }

    public async Task<McpToolResult> ExecuteAsync(
    Dictionary<string, object>? arguments,
    IRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var input = arguments?["input"]?.ToString();
     // Your implementation
        return McpToolResult.Success($"Processed: {input}");
    }
}
```

### 3. Register Your Tools

In your `Startup` class, override `ConfigureMcpServices`:

```csharp
protected override void ConfigureMcpServices(IServiceCollection services)
{
    // Register individual tools
    services.AddMcpTool<MyCalculatorTool>();
    services.AddMcpTool<CustomTool>();
    
    // Or register multiple tools at once
    services.AddMcpTools(
        typeof(MyCalculatorTool), 
        typeof(CustomTool)
    );
}
```

## MCP Endpoints

Once enabled, your service will expose the following MCP endpoints:

### Get Server Capabilities
```
GET /mcp/capabilities
```

Returns server information and supported capabilities.

### List Available Tools
```
POST /mcp/tools/list
```

Returns all available tools with their schemas.

### Execute Tool
```
POST /mcp/tools/call
```

Executes a specific tool with provided arguments.

Example request:
```json
{
    "method": "tools/call",
    "params": {
      "name": "calculate",
        "arguments": {
            "operation": "add",
  "operand1": 10,
  "operand2": 5
        }
    }
}
```

Example response:
```json
{
    "result": {
        "isSuccess": true,
        "content": [
  {
 "type": "text",
"text": "Result: 15"
      }
        ]
    }
}
```

### Health Check
```
GET /mcp/health
```

Returns server health status and tool count.

## Authentication

All MCP endpoints require JWT authentication. The service uses the "JwtOnly" policy, which means:

- API key authentication is not allowed for MCP endpoints
- Only valid JWT tokens are accepted
- The token must contain user ID and organization ID claims

Include the JWT token in the Authorization header:
```
Authorization: Bearer <your-jwt-token>
```

## Request Context

MCP tools automatically receive an `IRequestContext` containing:
- `UserId`: The authenticated user's ID
- `OrganizationId`: The user's organization ID  
- `CorrelationId`: Request correlation ID for tracking

## Tool Schema Generation

When using `McpToolBase`, schemas are automatically generated from your method signature:

- Method parameters become tool arguments
- `[Description]` attributes provide parameter descriptions
- Parameter types are mapped to JSON schema types
- Optional parameters (with default values) are marked as not required

## Error Handling

Tools should return `McpToolResult` objects:

```csharp
// Success
return McpToolResult.Success("Operation completed");
return McpToolResult.Success(listOfMcpContent);

// Failure
return McpToolResult.Failed("Error message");
```

## Best Practices

1. **Tool Naming**: Use descriptive, lowercase names with underscores (e.g., `get_user_info`)

2. **Descriptions**: Provide clear descriptions for tools and parameters

3. **Error Handling**: Always handle exceptions and return meaningful error messages

4. **Security**: Tools have access to user context - validate permissions appropriately

5. **Performance**: Consider caching for expensive operations

6. **Testing**: Test tools independently using the `ExecuteAsync` method

## Sample Service

The `Simplic.OxS.McpSample` project demonstrates:
- Basic MCP server setup
- Example tools (OrganizationInfoTool, CalculatorTool)
- Proper configuration and registration

## Integration with Existing Services

To add MCP to an existing service:

1. Add project references to `Simplic.OxS.Mcp` and `Simplic.OxS.Mcp.Server`
2. Override `IsMcpEnabled()` to return `true`
3. Override `ConfigureMcpServices()` to register your tools
4. The MCP endpoints will be automatically available under `/mcp/`

The MCP system integrates seamlessly with the existing Simplic.OxS infrastructure, using the same authentication, logging, and request context systems.
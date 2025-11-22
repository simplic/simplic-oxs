# Migration from Swagger to Scalar Documentation

This document describes the migration from Swagger/SwaggerUI to Scalar for API documentation in the Simplic.OxS framework, including comprehensive support for REST APIs, GraphQL, and SignalR WebSockets.

## Overview

Scalar provides a modern, fast, and beautiful API documentation experience that integrates seamlessly with OpenAPI specifications while offering enhanced features for documenting GraphQL and WebSocket endpoints.

## What's Changed

### ?? Package Changes

**Removed:**
- `Swashbuckle.AspNetCore` - Replaced with modern alternatives
- `SignalRSwaggerGen` - Integrated into custom Scalar documentation

**Added:**
- `Scalar.AspNetCore` (v1.2.42) - Modern API documentation UI
- `Microsoft.AspNetCore.OpenApi` (v8.0.2) - .NET 8 native OpenAPI support

### ?? Code Changes

1. **Bootstrap.cs**
   - Replaced `services.AddSwagger()` with `services.AddScalar()`
   - Replaced `app.UseSwagger()` and `app.UseSwaggerUI()` with `app.UseScalar()`
   - Added GraphQL integration with `GraphQLQueryType` property
   - Enhanced SignalR detection with `HasSignalRHubs()` method

2. **Extensions**
   - Removed `SwaggerExtension.cs`
   - Added `ScalarExtension.cs` with comprehensive configuration
   - Enhanced `GraphQLExtension.cs` for better documentation support

3. **Attributes**
   - Updated `HideInDocumentation` to work with multiple documentation systems

## ?? Documentation Features

### REST API Documentation
- **OpenAPI/Swagger compatible** - Maintains full compatibility with existing OpenAPI specifications
- **Interactive interface** - Modern, fast, and responsive documentation UI
- **Authentication support** - JWT Bearer and API Key authentication documented
- **Request/Response examples** - Comprehensive examples for all endpoints
- **Model schemas** - Detailed schema documentation for all data models

### GraphQL Documentation
- **Integrated GraphQL playground** - Access at `/graphql/ui` (development only)
- **Schema introspection** - Full GraphQL schema documentation
- **Query examples** - Sample queries and mutations
- **Type system documentation** - Complete type documentation
- **Real-time subscriptions** - WebSocket-based subscriptions support

### WebSocket Documentation (SignalR)
- **Hub documentation** - Automatic documentation of SignalR hubs
- **Method signatures** - Parameter and return type documentation
- **Connection examples** - JavaScript and .NET client examples
- **Authentication guidance** - JWT token integration examples
- **Group management** - Documentation for group-based messaging

## ?? Implementation Guide

### Basic Implementation

```csharp
public class YourBootstrap : Bootstrap
{
    protected override string ServiceName => "YourService";
    protected override Type? GraphQLQueryType => typeof(YourQuery); // Optional
  protected override bool HasSignalRHubs() => true; // If you have SignalR hubs

    protected override void RegisterServices(IServiceCollection services)
    {
      // Your service registrations
        
        // For GraphQL support
      services.UseGraphQL<YourQuery>();
    }

  protected override void MapHubs(IEndpointRouteBuilder builder)
    {
        // Map your SignalR hubs
        builder.MapHub<YourHub>("/hubs/your-hub");
    }
}
```

### REST API Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class YourController : OxSController
{
    /// <summary>
    /// Creates a new resource
/// </summary>
    /// <param name="request">Creation request</param>
    /// <returns>Created resource</returns>
    /// <response code="201">Resource created successfully</response>
    /// <response code="400">Invalid request data</response>
    [HttpPost]
    [ProducesResponseType(typeof(YourModel), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<YourModel>> Create([FromBody] CreateRequest request)
    {
        // Implementation
    }
}
```

### GraphQL Queries

```csharp
public class YourQuery : IQueryBase
{
    [Authorize]
    [UseProjection]
    [UseFiltering]
    [UsePaging]
    public async Task<IExecutable<YourModel>> GetItems(
        [Service] IYourService service)
 {
        return await service.GetItemsQueryable();
    }
}
```

### SignalR Hubs

```csharp
[Authorize]
public class YourHub : OxSHub<IYourClient>
{
 /// <summary>
    /// Send a message to all connected clients
    /// </summary>
 /// <param name="message">The message to send</param>
    public async Task SendToAll(string message)
    {
 await Clients.All.ReceiveMessage(message);
    }
}

public interface IYourClient
{
    Task ReceiveMessage(string message);
}
```

## ?? Customization

### Custom Styling

The Scalar documentation can be customized with CSS:

```csharp
// In ScalarExtension.cs
scalarOptions.WithCustomCss(@"
.scalar-app {
    --scalar-color-1: #your-primary-color;
    --scalar-color-2: #your-secondary-color;
}
");
```

### Additional Content

Add custom content sections:

```csharp
scalarOptions.WithContent(@"
## Custom Section
Your custom documentation content here.
");
```

## ?? Access Points

After implementing the migration, your documentation will be available at:

- **Main Documentation**: `/{service-name}-api/{version}/scalar/v1`
- **GraphQL Playground**: `/graphql` (if GraphQL is configured)
- **OpenAPI Spec**: `/openapi/v1.json`

## ?? Migration Checklist

- [ ] Update package references in `.csproj`
- [ ] Update `Bootstrap.cs` to use Scalar instead of Swagger
- [ ] Remove old `SwaggerExtension.cs` references
- [ ] Implement `GraphQLQueryType` property if using GraphQL
- [ ] Implement `HasSignalRHubs()` method if using SignalR
- [ ] Update any custom Swagger filters or configurations
- [ ] Test all documentation endpoints
- [ ] Update deployment scripts if they reference Swagger URLs

## ?? Breaking Changes

1. **URL Changes**: Documentation URLs have changed from `/swagger` to `/scalar`
2. **Configuration**: Swagger-specific configuration options are no longer available
3. **Filters**: Custom Swagger filters need to be reimplemented for Scalar

## ?? Troubleshooting

### Common Issues

1. **Missing Documentation**: Ensure XML documentation is enabled in your project
2. **GraphQL Not Showing**: Verify `GraphQLQueryType` property is set
3. **SignalR Not Documented**: Check `HasSignalRHubs()` returns true

### Getting Help

For issues specific to the Simplic.OxS framework migration:
1. Check the sample implementation in `Simplic.OxS.Server/Sample/`
2. Review the console output during startup for configuration issues
3. Contact the SIMPLIC development team

## ?? Benefits of Migration

1. **Performance**: Faster loading and rendering of documentation
2. **Modern UI**: Cleaner, more intuitive interface
3. **Better Integration**: Native support for multiple API types
4. **Maintenance**: Reduced dependencies and simpler configuration
5. **Future-Proof**: Built on modern web standards and actively maintained

The migration to Scalar provides a more comprehensive and maintainable documentation solution for the diverse API types used in the Simplic.OxS ecosystem.
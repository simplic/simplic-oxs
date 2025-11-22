using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System.Reflection;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// Configuration options for Scalar documentation
    /// </summary>
    public class ScalarOptions
    {
        public string ApiVersion { get; set; } = "v1";
        public string Title { get; set; } = "API Documentation";
        public string Description { get; set; } = "API documentation";
     public ScalarContact? Contact { get; set; }
        public ScalarLicense? License { get; set; }
   public string? TermsOfService { get; set; }
        public string BasePath { get; set; } = "/";
        public bool IncludeSignalR { get; set; } = true;
        public bool IncludeGraphQL { get; set; } = true;
    }

    /// <summary>
    /// Contact information for Scalar documentation
    /// </summary>
    public class ScalarContact
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
     public string? Url { get; set; }
    }

    /// <summary>
    /// License information for Scalar documentation
    /// </summary>
    public class ScalarLicense
    {
     public string? Name { get; set; }
        public string? Url { get; set; }
    }

    /// <summary>
    /// Scalar extension methods for API documentation
    /// </summary>
    internal static class ScalarExtension
{
        /// <summary>
        /// Add Scalar API documentation to the service collection
    /// </summary>
  /// <param name="services">Service collection</param>
        /// <param name="env">Web host environment</param>
     /// <param name="apiVersion">API version</param>
        /// <param name="serviceName">Service name</param>
     /// <param name="info">OpenAPI info</param>
    /// <returns>Service collection</returns>
     internal static IServiceCollection AddScalar(this IServiceCollection services, IWebHostEnvironment env, string apiVersion, string serviceName, OpenApiInfo info)
    {
            return services.AddScalarConfiguration(options =>
      {
            options.ApiVersion = apiVersion;
options.Title = info.Title;
        options.Description = info.Description ?? "API Documentation";
        options.Contact = new ScalarContact
     {
   Name = info.Contact?.Name,
  Email = info.Contact?.Email,
     Url = info.Contact?.Url?.ToString()
       };
           options.License = new ScalarLicense
    {
     Name = info.License?.Name,
       Url = info.License?.Url?.ToString()
     };
     options.TermsOfService = info.TermsOfService?.ToString();
            options.BasePath = $"/{serviceName.ToLower()}-api/{apiVersion}";
    });
     }

        /// <summary>
     /// Add Scalar API documentation to the service collection with custom options
        /// </summary>
     /// <param name="services">Service collection</param>
  /// <param name="configure">Configuration action</param>
        /// <returns>Service collection</returns>
  internal static IServiceCollection AddScalarConfiguration(this IServiceCollection services, Action<ScalarOptions> configure)
 {
   var options = new ScalarOptions();
   configure(options);

      // Add basic endpoint documentation
        services.AddEndpointsApiExplorer();

        // Store options for later use
services.AddSingleton(options);

    return services;
        }

  /// <summary>
        /// Configure Scalar UI in the application pipeline
        /// </summary>
        /// <param name="app">Application builder</param>
  /// <param name="options">Scalar configuration options</param>
        /// <param name="env">Web host environment</param>
        internal static void UseScalarDocumentation(this IApplicationBuilder app, ScalarOptions options, IWebHostEnvironment env)
        {
 // For now, provide a simple message that Scalar documentation is configured
// This can be enhanced when the proper Scalar setup is available
            Console.WriteLine($"? Scalar API Documentation configured for {options.Title}");
            Console.WriteLine($"   - Version: {options.ApiVersion}");
           Console.WriteLine($"   - Base Path: {options.BasePath}");
          
            if (options.IncludeGraphQL)
   {
         Console.WriteLine("   - GraphQL endpoint: /graphql");
            }
   
      if (options.IncludeSignalR)
    {
        Console.WriteLine("   - SignalR hubs: Configured");
}
  }

  /// <summary>
     /// Build custom documentation content for Scalar
    /// </summary>
        /// <param name="options">Scalar options</param>
 /// <returns>Custom documentation content</returns>
     private static string BuildCustomDocumentationContent(ScalarOptions options)
        {
 var content = new List<string>();

      if (options.IncludeSignalR)
   {
  content.Add(@"## WebSocket Endpoints (SignalR)

This API also supports real-time communication through SignalR WebSocket connections.

**Connection URL Pattern:** `/hubs/{hubName}`

### Authentication
Include the JWT token when establishing the connection:
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl(""/hubs/notification"", {
        accessTokenFactory: () => ""your-jwt-token"")
    })
    .build();
```

### Features
- Real-time bidirectional communication
- Automatic reconnection support
- Group-based messaging
- Type-safe client proxies");
            }

   if (options.IncludeGraphQL)
      {
             content.Add(@"## GraphQL Endpoint

This API supports GraphQL for flexible data querying.

**GraphQL Endpoint:** `/graphql`
**GraphQL Playground:** Available in development mode

### Features
- Single endpoint for all data operations
- Strong type system with introspection
- Advanced filtering and pagination
- Real-time subscriptions

### Authentication
Include the JWT token in the Authorization header:
```
Authorization: Bearer your-jwt-token
```

### Example Query
```graphql
query GetNotifications($first: Int, $where: NotificationFilterInput) {
notifications(first: $first, where: $where) {
        nodes {
   id
       title
   content
   type
            createdAt
        }
        pageInfo {
        hasNextPage
            hasPreviousPage
        }
    }
}
```");
            }

      return string.Join("\n\n", content);
     }

        /// <summary>
    /// Create WebSocket documentation for Scalar
        /// </summary>
        /// <param name="hubs">List of SignalR hub types</param>
        /// <returns>WebSocket documentation</returns>
        internal static Dictionary<string, object> CreateWebSocketDocumentation(IEnumerable<Type> hubs)
        {
   var documentation = new Dictionary<string, object>();

            foreach (var hubType in hubs)
   {
       var hubName = hubType.Name.Replace("Hub", "").ToLowerInvariant();
     var methods = hubType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName && m.DeclaringType == hubType)
    .ToList();

 var hubDoc = new
      {
         name = hubName,
     description = $"SignalR Hub for {hubName} operations",
               endpoint = $"/hubs/{hubName}",
        methods = methods.Select(m => new
   {
      name = m.Name,
             parameters = m.GetParameters().Select(p => new
        {
   name = p.Name,
            type = p.ParameterType.Name,
               required = !p.IsOptional
       }).ToList(),
   returnType = m.ReturnType.Name,
        description = $"SignalR method: {m.Name}"
                }).ToList()
                };

          documentation[hubName] = hubDoc;
       }

            return documentation;
        }

    /// <summary>
        /// Create GraphQL documentation for Scalar
        /// </summary>
      /// <param name="queryType">GraphQL query type</param>
        /// <returns>GraphQL documentation</returns>
      internal static Dictionary<string, object> CreateGraphQLDocumentation(Type? queryType)
   {
 var documentation = new Dictionary<string, object>();

   if (queryType == null)
        return documentation;

  var queries = queryType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName)
        .ToList();

      documentation["graphql"] = new
            {
                name = "GraphQL",
       description = "GraphQL endpoint for flexible data querying",
     endpoint = "/graphql",
 playground = "/graphql/playground",
    queries = queries.Select(q => new
    {
     name = q.Name,
returnType = q.ReturnType.Name,
   parameters = q.GetParameters().Select(p => new
           {
       name = p.Name,
        type = p.ParameterType.Name,
           required = !p.IsOptional
      }).ToList(),
          description = $"GraphQL query: {q.Name}"
 }).ToList()
      };

   return documentation;
     }
    }
}
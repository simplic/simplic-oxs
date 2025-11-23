using System.ComponentModel;
using System.Reflection;

namespace Simplic.OxS.Mcp;

/// <summary>
/// Attribute to mark a method as an MCP tool
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class McpToolAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the McpToolAttribute class
    /// </summary>
    /// <param name="name">Tool name</param>
    /// <param name="description">Tool description</param>
    public McpToolAttribute(string name, string? description = null)
    {
        Name = name;
        Description = description;
    }

    /// <summary>
    /// Gets the tool name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the tool description
    /// </summary>
    public string? Description { get; }
}

/// <summary>
/// Base class for implementing MCP tools using method-based definitions
/// </summary>
public abstract class McpToolBase : IMcpTool
{
    /// <inheritdoc />
    public virtual McpTool GetToolDefinition()
    {
        var method = GetToolMethod();
        var attribute = method.GetCustomAttribute<McpToolAttribute>()
         ?? throw new InvalidOperationException($"Method {method.Name} must have McpToolAttribute");

        return new McpTool
        {
            Name = attribute.Name,
            Description = attribute.Description,
            InputSchema = GenerateInputSchema(method)
        };
    }

    /// <inheritdoc />
    public abstract Task<McpToolResult> ExecuteAsync(IDictionary<string, object>? arguments
                                                   , IRequestContext context
                                                   , CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the method that implements this tool
    /// </summary>
    protected virtual MethodInfo GetToolMethod()
    {
        return GetType()
                 .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
         .FirstOrDefault(m => m.GetCustomAttribute<McpToolAttribute>() != null)
                 ?? throw new InvalidOperationException("No method with McpToolAttribute found");
    }

    /// <summary>
    /// Generates input schema for a method
    /// </summary>
    protected virtual McpToolSchema GenerateInputSchema(MethodInfo method)
    {
        var schema = new McpToolSchema { Type = "object" };
        var properties = new Dictionary<string, McpSchemaProperty>();
        var required = new List<string>();

        var parameters = method.GetParameters()
   .Where(p => p.ParameterType != typeof(IRequestContext) &&
         p.ParameterType != typeof(CancellationToken));

        foreach (var param in parameters)
        {
            var property = CreateSchemaProperty(param);
            properties[param.Name!] = property;

            if (!param.HasDefaultValue && !IsNullable(param.ParameterType))
            {
                required.Add(param.Name!);
            }
        }

        schema.Properties = properties;
        if (required.Any())
        {
            schema.Required = required;
        }

        return schema;
    }

    /// <summary>
    /// Creates a schema property for a parameter
    /// </summary>
    protected virtual McpSchemaProperty CreateSchemaProperty(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        var property = new McpSchemaProperty
        {
            Type = GetJsonType(type),
            Description = parameter.GetCustomAttribute<DescriptionAttribute>()?.Description
        };

        if (parameter.HasDefaultValue)
        {
            property.Default = parameter.DefaultValue;
        }

        return property;
    }

    /// <summary>
    /// Gets the JSON type name for a .NET type
    /// </summary>
    protected virtual string GetJsonType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType switch
        {
            var t when t == typeof(string) => "string",
            var t when t == typeof(int) || t == typeof(long) || t == typeof(short) => "integer",
            var t when t == typeof(double) || t == typeof(float) || t == typeof(decimal) => "number",
            var t when t == typeof(bool) => "boolean",
            var t when t.IsArray || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) => "array",
            var t when t == typeof(DateTime) || t == typeof(DateTimeOffset) => "string",
            var t when t == typeof(Guid) => "string",
            _ => "object"
        };
    }

    /// <summary>
    /// Checks if a type is nullable
    /// </summary>
    protected static bool IsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
}
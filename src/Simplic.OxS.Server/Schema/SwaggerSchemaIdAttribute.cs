using System.Reflection;

namespace Simplic.OxS.Server;

/// <summary>
/// Attribute to rename a model in the swagger schema.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum)]
public sealed class SwaggerSchemaIdAttribute : Attribute
{
    /// <summary>
    /// Get the schema id of a model type.
    /// </summary>
    /// <param name="type">The type of the model.</param>
    /// <returns>The swagger id of the model type.</returns>
    /// <exception cref="Exception">The id has an invalid format.</exception>
    public static string GetSchemaId(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var attribute = type.GetCustomAttribute<SwaggerSchemaIdAttribute>();

        var id = attribute?.Id ?? type.Name;
        if (string.IsNullOrWhiteSpace(id))
            throw new Exception("Invalid schema id");

        return id;
    }

    /// <summary>
    /// Create a new <see cref="SwaggerSchemaIdAttribute"/> with a given id.
    /// </summary>
    public SwaggerSchemaIdAttribute(string id)
    {
        Id = id;
    }

    /// <summary>
    /// The swagger id of the model.
    /// </summary>
    public string Id { get; }
}

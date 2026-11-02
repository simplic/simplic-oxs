using System.Reflection;

namespace Simplic.OxS.Server;

/// <summary>
/// Attribute to rename a model in the swagger schema.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum)]
public sealed class SwaggerSchemaIdAttribute : Attribute
{
    /// <summary>
    /// The default swagger schema id selector.
    /// From https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/04bb28d7739fdea826198a8916396ce4df2550d4/src/Swashbuckle.AspNetCore.SwaggerGen/SchemaGenerator/SchemaGeneratorOptions.cs#L43
    /// </summary>
    public static string DefaultSchemaIdSelector(Type modelType)
    {
        if (!modelType.IsConstructedGenericType)
        {
            return modelType.Name.Replace("[]", "Array");
        }

        var prefix = modelType.GetGenericArguments()
            .Select(DefaultSchemaIdSelector)
            .Aggregate((previous, current) => previous + current);

        return prefix + modelType.Name.Split('`').First();
    }

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
        if (attribute != null)
            return attribute.Id;

        return DefaultSchemaIdSelector(type);
    }

    /// <summary>
    /// Create a new <see cref="SwaggerSchemaIdAttribute"/> with a given id.
    /// </summary>
    public SwaggerSchemaIdAttribute(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
    }

    /// <summary>
    /// The swagger id of the model.
    /// </summary>
    public string Id { get; }
}

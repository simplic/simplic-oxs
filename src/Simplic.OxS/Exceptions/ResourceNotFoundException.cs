using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS;

/// <summary>
/// Exception thrown when a referenced resource does not exist.
/// </summary>
public class ResourceNotFoundException : Exception
{
    /// <summary>
    /// Helper to check for null resources. Throws if <paramref name="resource"/> is null.
    /// </summary>
    /// <returns>Returns <paramref name="resource"/>.</returns>
    public static T ExpectNotNull<T>([NotNull] T? resource, object? id)
    {
        if (resource is null)
            throw FromType<T>(id);
        else
            return resource;
    }

    /// <summary>
    /// Create an exception with type <typeparamref name="T"/> and id <paramref name="id"/>.
    /// </summary>
    public static ResourceNotFoundException FromType<T>(object? id)
    {
        return FromType(typeof(T), id);
    }

    /// <summary>
    /// Create an exception with type <paramref name="type"/> and id <paramref name="id"/>.
    /// </summary>
    public static ResourceNotFoundException FromType(Type type, object? id)
    {
        return new ResourceNotFoundException(type.Name, id);
    }

    /// <summary>
    /// Create a new <see cref="ResourceNotFoundException"/>.
    /// </summary>
    /// <param name="type">The type of the missing resource.</param>
    /// <param name="id">The id of the missing resource.</param>
    public ResourceNotFoundException(string type, object? id) : base($"Resource of type '{type}' with id '{id}' could not be found.")
    {
        Type = type;
        Id = id;
    }

    /// <summary>
    /// Simple name of the resource type.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// The id of the resource (e.g. a name: <see cref="string"/> or guid: <see cref="Guid"/>).
    /// </summary>
    public object? Id { get; }
}

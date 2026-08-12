using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Exceptions;

/// <summary>
/// Exception thrown when a referenced resource does not exist and publishes the resource type and id
/// as machine-readable problem-details members. Maps to an HTTP <c>404 Not Found</c> response.
/// <para>
/// Deprecated: prefer the anonymous <see cref="NotFoundException"/>. Echoing the resource type and id
/// back to the caller lets an unauthorized client distinguish "this resource does not exist" from
/// "it exists but is not yours" (or from an invalid route), which leaks the existence of foreign ids.
/// A plain <see cref="NotFoundException"/> keeps those cases indistinguishable. This type is retained
/// only for administrative/owner-verified lookups that already prove the caller may know the resource,
/// and will be removed in a future major version.
/// </para>
/// </summary>
[Obsolete("Prefer the anonymous NotFoundException, which does not reveal whether a resource exists or a route is invalid. ResourceNotFoundException will be removed in a future major version.")]
public class ResourceNotFoundException : NotFoundException
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
    public ResourceNotFoundException(string type, object? id)
        : base($"Resource of type '{type}' with id '{id}' could not be found.")
    {
        Type = type;
        Id = id;
    }

    /// <inheritdoc/>
    public override void PopulateProblemDetails(IDictionary<string, object?> extensions)
    {
        // Preserve the legacy "Type@id" identifier while also exposing the parts separately.
        extensions["resource"] = Id is null ? Type : $"{Type}@{Id}";
        extensions["resourceType"] = Type;
        extensions["resourceId"] = Id;
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

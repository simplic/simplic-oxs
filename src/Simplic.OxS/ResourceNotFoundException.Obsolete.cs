using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS;

/// <summary>
/// Deprecated alias for <see cref="Simplic.OxS.Exceptions.ResourceNotFoundException"/>, kept so
/// existing callers in the <c>Simplic.OxS</c> namespace keep compiling during the deprecation cycle.
/// <para>
/// New code should use <see cref="Simplic.OxS.Exceptions.ResourceNotFoundException"/> directly. This
/// alias — including its <see cref="FromType{T}(object?)"/> / <see cref="ExpectNotNull{T}(T, object?)"/>
/// factories, which intentionally return this type for backward-compatible <c>catch</c> blocks — will
/// be removed in a future major version.
/// </para>
/// </summary>
[Obsolete("Use Simplic.OxS.Exceptions.ResourceNotFoundException instead. This alias will be removed in a future major version.")]
#pragma warning disable CS0618 // canonical base is itself obsolete; this alias forwards to it during the deprecation cycle
public class ResourceNotFoundException : Simplic.OxS.Exceptions.ResourceNotFoundException
#pragma warning restore CS0618
{
    /// <summary>
    /// Create a new <see cref="ResourceNotFoundException"/>.
    /// </summary>
    /// <param name="type">The type of the missing resource.</param>
    /// <param name="id">The id of the missing resource.</param>
    public ResourceNotFoundException(string type, object? id) : base(type, id)
    {
    }

    /// <summary>
    /// Helper to check for null resources. Throws if <paramref name="resource"/> is null.
    /// </summary>
    /// <returns>Returns <paramref name="resource"/>.</returns>
    public static new T ExpectNotNull<T>([NotNull] T? resource, object? id)
    {
        if (resource is null)
            throw FromType<T>(id);

        return resource;
    }

    /// <summary>
    /// Create an exception with type <typeparamref name="T"/> and id <paramref name="id"/>.
    /// </summary>
    public static new ResourceNotFoundException FromType<T>(object? id)
        => new(typeof(T).Name, id);

    /// <summary>
    /// Create an exception with type <paramref name="type"/> and id <paramref name="id"/>.
    /// </summary>
    public static new ResourceNotFoundException FromType(Type type, object? id)
        => new(type.Name, id);
}

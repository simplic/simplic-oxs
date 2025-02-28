namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions;

/// <summary>
/// Attribute for properties that reference the object in other objects.
/// Like the ID or primary key.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ReferencePropertyAttribute : Attribute
{
}
namespace Simplic.OxS.ModelDefinition.Extenstion.Abstractions;

/// <summary>
/// Marks a internal property for the model definition.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class InternalPropertyAttribute : Attribute
{
}

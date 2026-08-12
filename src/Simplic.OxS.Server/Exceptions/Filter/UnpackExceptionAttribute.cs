namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Use to annotate exceptions as unpackable (allow the inner exception to be accessed) when handled
/// by the global exception-handler chain via <see cref="ExceptionUnpacker"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class UnpackExceptionAttribute : Attribute
{ }

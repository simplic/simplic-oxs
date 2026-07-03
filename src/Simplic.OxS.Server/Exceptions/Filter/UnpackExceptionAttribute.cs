namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Use to annotate excxeptions to be unpackable (allow the inner exception to be accessed) when handled by <see cref="CommonExceptionFilterAttribute{TException}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class UnpackExceptionAttribute : Attribute
{ }

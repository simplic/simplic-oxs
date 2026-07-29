using Microsoft.AspNetCore.Mvc.Filters;

namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Exception filter for <see cref="ResourceNotFoundException"/>.
/// </summary>
/// <remarks>
/// Redundant since <see cref="OxSExceptionHandler"/> handles <see cref="ResourceNotFoundException"/>
/// globally for every controller. This filter also responds with a bare
/// <c>"Type@id"</c> string rather than RFC 7807 <c>ProblemDetails</c>, so controllers still
/// carrying it return a different error shape from the rest of the fleet. Remove the attribute
/// to adopt the standard contract, which exposes the same information as the
/// <c>resourceType</c> and <c>resourceId</c> members.
/// </remarks>
[Obsolete(
    "Redundant: OxSExceptionHandler handles ResourceNotFoundException globally and returns "
    + "RFC 7807 ProblemDetails with resourceType/resourceId. This filter returns a bare "
    + "\"Type@id\" string instead, which is inconsistent with every other endpoint. "
    + "Remove the attribute from the controller.")]
public class ResourceNotFoundExceptionFilterAttribute : CommonExceptionFilterAttribute<ResourceNotFoundException>
{
    /// <inheritdoc/>
    protected override void HandleException(ExceptionContext context, ResourceNotFoundException exception)
    {
        context.Result = Results.NotFound(exception.Type, exception.Id);
    }
}

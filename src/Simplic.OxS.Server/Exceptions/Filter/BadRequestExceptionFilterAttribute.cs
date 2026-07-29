using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Exception filter for <see cref="BadRequestException"/>.
/// </summary>
/// <remarks>
/// Redundant since <see cref="OxSExceptionHandler"/> handles <see cref="BadRequestException"/>
/// globally for every controller. This filter also responds with a bare string body rather than
/// RFC 7807 <c>ProblemDetails</c>, so controllers still carrying it return a different error
/// shape from the rest of the fleet. Remove the attribute to adopt the standard contract.
/// </remarks>
[Obsolete(
    "Redundant: OxSExceptionHandler handles BadRequestException globally and returns RFC 7807 "
    + "ProblemDetails. This filter returns a bare string instead, which is inconsistent with "
    + "every other endpoint. Remove the attribute from the controller.")]
public class BadRequestExceptionFilterAttribute : CommonExceptionFilterAttribute<BadRequestException>
{
    /// <inheritdoc/>
    protected override void HandleException(ExceptionContext context, BadRequestException exception)
    {
        context.Result = new BadRequestObjectResult(exception.Message);
    }
}

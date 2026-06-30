using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Simplic.OxS.Server.Exceptions;

namespace Simplic.OxS.Server;

/// <summary>
/// Exception filter for <see cref="BadRequestException"/>.
/// </summary>
public class BadRequestExceptionFilterAttribute : CommonExceptionFilterAttribute<BadRequestException>
{
    /// <inheritdoc/>
    protected override void HandleException(ExceptionContext context, BadRequestException exception)
    {
        context.Result = new BadRequestObjectResult(exception.Message);
    }
}

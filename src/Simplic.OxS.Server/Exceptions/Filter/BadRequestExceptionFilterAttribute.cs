using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Simplic.OxS.Server;

public class BadRequestExceptionFilterAttribute : ExceptionFilterAttribute
{
    /// <inheritdoc/>
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is BadRequestException exception)
            context.Result = new BadRequestObjectResult(exception.Message);
    }
}

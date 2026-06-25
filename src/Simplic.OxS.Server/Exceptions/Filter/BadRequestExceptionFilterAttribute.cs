using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Simplic.OxS.Server.Exceptions;

namespace Simplic.OxS.Server;

public class BadRequestExceptionFilterAttribute : CommonExceptionFilterAttribute<BadRequestException>
{
    protected override void HandleException(ExceptionContext context, BadRequestException exception)
    {
        context.Result = new BadRequestObjectResult(exception.Message);
    }
}

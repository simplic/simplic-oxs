using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Simplic.OxS.Server;

public class ResourceNotFoundExceptionFilterAttribute : ExceptionFilterAttribute
{
    /// <inheritdoc/>
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is ResourceNotFoundException exception)
            context.Result = new NotFoundObjectResult(exception.Message);
    }
}

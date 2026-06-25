using Microsoft.AspNetCore.Mvc.Filters;
using Simplic.OxS.Server.Exceptions;

namespace Simplic.OxS.Server;

public class ResourceNotFoundExceptionFilterAttribute : CommonExceptionFilterAttribute<ResourceNotFoundException>
{
    protected override void HandleException(ExceptionContext context, ResourceNotFoundException exception)
    {
        context.Result = Results.NotFound(exception.Type, exception.Id);
    }
}

using Microsoft.AspNetCore.Mvc.Filters;

namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Exception filter for <see cref="ResourceNotFoundException"/>.
/// </summary>
public class ResourceNotFoundExceptionFilterAttribute : CommonExceptionFilterAttribute<ResourceNotFoundException>
{
    /// <inheritdoc/>
    protected override void HandleException(ExceptionContext context, ResourceNotFoundException exception)
    {
        context.Result = Results.NotFound(exception.Type, exception.Id);
    }
}

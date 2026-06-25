using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace Simplic.OxS.Server.Exceptions;

public class CommonExceptionFilterAttribute<TException> : ExceptionFilterAttribute where TException : Exception
{
    public override Task OnExceptionAsync(ExceptionContext context)
    {
        for (var exception = context.Exception; exception != null; exception = exception.InnerException)
        {
            if (exception is TException targetException)
                return HandleExceptionAsync(context, targetException);

            if (exception.GetType().GetCustomAttribute<UnpackExceptionAttribute>() == null)
                break;
        }

        return Task.CompletedTask;
    }

    protected virtual Task HandleExceptionAsync(ExceptionContext context, TException exception)
    {
        HandleException(context, exception);

        return Task.CompletedTask;
    }

    protected virtual void HandleException(ExceptionContext context, TException exception)
    { }
}

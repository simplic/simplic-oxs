using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Common base filter for exceptions.
/// </summary>
/// <typeparam name="TException"></typeparam>
public class CommonExceptionFilterAttribute<TException> : ExceptionFilterAttribute where TException : Exception
{
    /// <inheritdoc/>
    public override void OnException(ExceptionContext context)
    {
        if (TryGetException(context, out var exception))
            HandleException(context, exception);
    }

    /// <summary>
    /// Try to get the targeted exception from the context.
    /// </summary>
    protected bool TryGetException(ExceptionContext context, [NotNullWhen(true)] out TException? target)
    {
        for (var exception = context.Exception; exception != null; exception = UnpackException(exception))
        {
            if (exception is TException targetException)
            {
                target = targetException;
                return true;
            }
        }

        target = null;
        return false;
    }

    /// <summary>
    /// Unpacks an inner exception.
    /// </summary>
    /// <param name="exception">The exception to unpack.</param>
    /// <returns>The unpacked exception or null if there is no packaged exception.</returns>
    protected virtual Exception? UnpackException(Exception exception)
    {
        if (exception.GetType().GetCustomAttribute<UnpackExceptionAttribute>() == null)
            return null;
        else
            return exception.InnerException;
    }

    /// <summary>
    /// Handler for the targeted exception.
    /// </summary>
    /// <param name="context">The exception context.</param>
    /// <param name="exception">The targeted exception.</param>
    protected virtual void HandleException(ExceptionContext context, TException exception)
    { }
}

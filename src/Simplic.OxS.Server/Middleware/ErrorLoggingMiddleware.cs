using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.Middleware;

/// <summary>
/// Middleware to log every unhandled exception.
/// </summary>
/// <remarks>
/// No longer registered by <see cref="Bootstrap"/>. It logged the exception and rethrew it
/// without the request path, status code, correlation id or organization id, and then let the
/// exception escape the pipeline — which is why unhandled failures surfaced as a bare
/// <c>500</c> with an empty body.
/// <para>
/// <c>Simplic.OxS.Server.Exceptions.OxSExceptionHandler</c> replaces it: it logs the same
/// exception with full request context and writes an RFC 7807 <c>ProblemDetails</c> response.
/// Registering this middleware as well would log every failure twice.
/// </para>
/// </remarks>
[Obsolete(
    "Superseded by OxSExceptionHandler, which is registered globally by Bootstrap and logs "
    + "with full request context. Registering this middleware causes duplicate error logs. "
    + "Remove the app.UseMiddleware<ErrorLoggingMiddleware>() call.")]
public class ErrorLoggingMiddleware(RequestDelegate next, ILogger<ErrorLoggingMiddleware> logger)
{
    /// <summary>
    /// invokes the middleware and logs an exception if any unhandled is thrown.
    /// </summary>
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            var message = e.Message;
            logger.LogError(e, "The following error happened: {message}", message);
            throw;
        }
    }
}

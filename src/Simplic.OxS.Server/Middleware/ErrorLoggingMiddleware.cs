using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.Middleware;

/// <summary>
/// Middleware to log every unhandled exception.
/// </summary>
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

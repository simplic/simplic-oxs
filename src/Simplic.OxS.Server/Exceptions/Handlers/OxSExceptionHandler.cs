using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Exceptions;

namespace Simplic.OxS.Server.Exceptions.Handlers;

/// <summary>
/// Global handler for every <see cref="OxSException"/>. Reads the HTTP metadata carried by the
/// exception (status, title, type, extension members and headers) and writes the matching RFC 9457
/// <c>application/problem+json</c> response. Logs with level attribution: 5xx as
/// <see cref="LogLevel.Error"/>, 401/403 as <see cref="LogLevel.Warning"/> and every other 4xx as
/// <see cref="LogLevel.Information"/>, so caller mistakes don't pollute the error rate.
/// </summary>
public sealed class OxSExceptionHandler(ILogger<OxSExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!ExceptionUnpacker.TryUnpack<OxSException>(exception, out var oxsException))
            return false;

        Log(httpContext, oxsException);

        var problemDetails = ProblemDetailsResponseWriter.Create(
            httpContext,
            oxsException.StatusCode,
            oxsException.Title,
            oxsException.ProblemType,
            oxsException.Detail);

        oxsException.PopulateProblemDetails(problemDetails.Extensions);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        oxsException.PopulateHeaders(headers);

        await ProblemDetailsResponseWriter.WriteAsync(
            httpContext,
            problemDetails,
            headers.Count > 0 ? headers : null,
            cancellationToken);

        return true;
    }

    private void Log(HttpContext httpContext, OxSException exception)
    {
        var level = exception.StatusCode switch
        {
            >= 500 => LogLevel.Error,
            StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden => LogLevel.Warning,
            _ => LogLevel.Information
        };

        logger.Log(
            level,
            exception,
            "Request {Method} {Path} failed with {StatusCode} ({ExceptionType})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            exception.StatusCode,
            exception.GetType().Name);
    }
}

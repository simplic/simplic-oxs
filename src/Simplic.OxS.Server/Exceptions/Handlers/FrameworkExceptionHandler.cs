using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.Exceptions.Handlers;

/// <summary>
/// Global handler for well-known framework exceptions that would otherwise surface as an opaque
/// <c>500</c>. Maps request-body-size failures to <c>413</c>, and treats a client that disconnects
/// mid-request as <c>499 (client closed request)</c> — logged at <see cref="LogLevel.Debug"/> and
/// kept out of the 5xx error rate — rather than a server fault.
/// </summary>
public sealed class FrameworkExceptionHandler(ILogger<FrameworkExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Non-standard status code used by nginx/Kestrel deployments for "client closed request".</summary>
    private const int ClientClosedRequest = 499;

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Client disconnected mid-request: not a server fault, don't inflate the 5xx rate.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug(
                "Request {Method} {Path} was cancelled by the client (499)",
                httpContext.Request.Method,
                httpContext.Request.Path);

            if (!httpContext.Response.HasStarted)
                httpContext.Response.StatusCode = ClientClosedRequest;

            return true;
        }

        var mapped = Map(exception);
        if (mapped is null)
            return false;

        var (statusCode, detail) = mapped.Value;

        logger.LogInformation(
            "Request {Method} {Path} failed with {StatusCode} ({ExceptionType})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            statusCode,
            exception.GetType().Name);

        var problemDetails = ProblemDetailsResponseWriter.Create(httpContext, statusCode, title: null, type: null, detail);

        await ProblemDetailsResponseWriter.WriteAsync(httpContext, problemDetails, headers: null, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Detail)? Map(Exception exception) => exception switch
    {
        // Kestrel raises this when the request body exceeds the configured size limit (413),
        // or for a malformed request (400). Honour the status it already carries.
        BadHttpRequestException badRequest => (
            badRequest.StatusCode,
            badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? "The request body is larger than the allowed limit."
                : "The request could not be processed."),

        // The multipart form reader raises this when the body exceeds MultipartBodyLengthLimit.
        InvalidDataException => (
            StatusCodes.Status413PayloadTooLarge,
            "The request body is larger than the allowed limit."),

        _ => null
    };
}

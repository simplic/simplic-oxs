using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.Exceptions.Handlers;

/// <summary>
/// Last handler in the chain: catches every exception no earlier handler recognised. Always logs it
/// at <see cref="LogLevel.Error"/> with the full exception.
/// <para>
/// In Development, Staging and Local it declines to handle (returns <see langword="false"/>) so the
/// developer exception page can still render the stack trace. In every other environment it writes a
/// fixed, generic <c>500</c> <c>application/problem+json</c> body — never leaking the exception
/// message or stack to the client.
/// </para>
/// </summary>
public sealed class FallbackExceptionHandler(
    IHostEnvironment environment,
    ILogger<FallbackExceptionHandler> logger) : IExceptionHandler
{
    private const string GenericDetail = "An unexpected error occurred while processing the request.";

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception on {Method} {Path} ({ExceptionType})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            exception.GetType().Name);

        // Let the developer exception page own the presentation in dev-facing environments.
        if (IsDeveloperExceptionPageEnvironment())
            return false;

        var problemDetails = ProblemDetailsResponseWriter.Create(
            httpContext,
            StatusCodes.Status500InternalServerError,
            title: null,
            type: null,
            GenericDetail);

        await ProblemDetailsResponseWriter.WriteAsync(httpContext, problemDetails, headers: null, cancellationToken);

        return true;
    }

    private bool IsDeveloperExceptionPageEnvironment()
        => environment.IsDevelopment()
           || environment.IsStaging()
           || environment.IsEnvironment("Local");
}

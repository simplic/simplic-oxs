using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Diagnostics;

namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Builds and writes RFC 9457 <c>application/problem+json</c> responses for the global
/// exception-handler chain.
/// <para>
/// The body is written deterministically regardless of the request's <c>Accept</c> header, so
/// every error surfaces the same machine-readable contract. Common members (<c>instance</c>,
/// <c>traceId</c>) are stamped centrally.
/// </para>
/// </summary>
internal static class ProblemDetailsResponseWriter
{
    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> pre-filled with status, title, type and the common
    /// <c>instance</c> / <c>traceId</c> members for the current request.
    /// </summary>
    public static ProblemDetails Create(HttpContext context, int statusCode, string? title, string? type, string? detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title ?? ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = detail,
            Type = type ?? "about:blank",
            Instance = context.Request.Path
        };

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        if (!string.IsNullOrEmpty(traceId))
            problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
    }

    /// <summary>
    /// Writes <paramref name="problemDetails"/> as <c>application/problem+json</c>, applying the
    /// optional <paramref name="headers"/> to the response first.
    /// </summary>
    public static async ValueTask WriteAsync(
        HttpContext context,
        ProblemDetails problemDetails,
        IDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        if (headers is not null)
        {
            foreach (var header in headers)
                context.Response.Headers[header.Key] = header.Value;
        }

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }
}

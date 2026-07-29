using HotChocolate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.GraphQL;

/// <summary>
/// Maps exceptions raised inside GraphQL resolvers onto the same error vocabulary the REST
/// endpoints use, so a client sees one consistent contract regardless of which surface it called.
/// <para>
/// Each error gains <c>errorCode</c>, <c>correlationId</c> and <c>traceId</c> extensions,
/// matching <c>Simplic.OxS.Server.Exceptions.OxSExceptionHandler</c>. Unrecognised exceptions are
/// reported as <c>unhandled</c> with a generic message — the detail goes to the log, keyed by
/// correlation id, and is never returned to the caller outside development.
/// </para>
/// </summary>
public sealed class OxSErrorFilter(
    ILogger<OxSErrorFilter> logger,
    IHttpContextAccessor httpContextAccessor) : IErrorFilter
{
    /// <inheritdoc/>
    public IError OnError(IError error)
    {
        var exception = error.Exception;

        var correlationId = httpContextAccessor.HttpContext?
            .Request.Headers[Constants.HttpHeaderCorrelationIdKey].FirstOrDefault();

        var traceId = System.Diagnostics.Activity.Current?.Id
                      ?? httpContextAccessor.HttpContext?.TraceIdentifier;

        // Validation and syntax errors raised by HotChocolate itself carry no exception and are
        // already client-actionable. Annotate them for correlation but otherwise leave them alone.
        if (exception is null)
            return WithDiagnostics(error, error.Code ?? "graphql_error", correlationId, traceId);

        if (exception is IOxSException oxs)
        {
            logger.LogWarning(
                exception,
                "GraphQL request failed: {ErrorCode} corr={CorrelationId} trace={TraceId} path={Path}",
                oxs.ErrorCode, correlationId, traceId, error.Path?.ToString());

            var mapped = error
                .WithMessage(exception.Message)
                .WithCode(oxs.ErrorCode);

            foreach (var (key, value) in oxs.ProblemExtensions)
                mapped = mapped.SetExtension(key, value);

            return WithDiagnostics(mapped, oxs.ErrorCode, correlationId, traceId);
        }

        // A client disconnecting mid-query is not a fault.
        if (exception is OperationCanceledException)
        {
            logger.LogDebug(
                "GraphQL request cancelled by client. corr={CorrelationId} trace={TraceId}",
                correlationId, traceId);

            return WithDiagnostics(
                error.WithMessage("The request was cancelled by the client.").WithCode("client_closed_request"),
                "client_closed_request", correlationId, traceId);
        }

        logger.LogError(
            exception,
            "GraphQL request failed: unhandled corr={CorrelationId} trace={TraceId} path={Path}",
            correlationId, traceId, error.Path?.ToString());

        return WithDiagnostics(
            error
                .WithMessage("An unexpected error occurred while processing the request.")
                .WithCode("unhandled"),
            "unhandled", correlationId, traceId);
    }

    /// <summary>
    /// Attaches the diagnostic members every Simplic OxS error carries.
    /// </summary>
    private static IError WithDiagnostics(IError error, string errorCode, string? correlationId, string? traceId)
        => error
            .SetExtension("errorCode", errorCode)
            .SetExtension("correlationId", correlationId)
            .SetExtension("traceId", traceId);
}

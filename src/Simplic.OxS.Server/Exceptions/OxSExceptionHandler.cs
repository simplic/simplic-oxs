using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simplic.OxS.InternalClient;
using Simplic.OxS.Settings.Organization.Exceptions;

namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Global exception handler that converts every unhandled exception into an RFC 7807
/// <see cref="ProblemDetails"/> response.
/// <para>
/// Registered once by <see cref="Bootstrap"/>, so every service gets a single, consistent
/// error contract with no per-controller wiring. Before this existed, an unhandled exception
/// escaped the pipeline entirely and Kestrel returned a bare <c>500</c> with an empty body,
/// which gave clients nothing to act on.
/// </para>
/// <para>
/// Every response carries <c>errorCode</c>, <c>correlationId</c> and <c>traceId</c>. The
/// <c>correlationId</c> is guaranteed to appear in the server logs for the same request, so a
/// client can quote it in a support request and it can be found directly.
/// </para>
/// </summary>
public sealed class OxSExceptionHandler(
    ILogger<OxSExceptionHandler> logger,
    IProblemDetailsService problemDetailsService,
    IWebHostEnvironment environment) : IExceptionHandler
{
    /// <summary>
    /// URI scheme used for the <c>ProblemDetails.Type</c> member. Deliberately a URN rather
    /// than an <c>https://</c> URL — the error codes are a stable contract, not a website.
    /// </summary>
    private const string ErrorTypeUrnPrefix = "urn:simplic-oxs:error:";

    /// <summary>
    /// Status code used when the client disconnected before the response was produced.
    /// Non-standard but widely understood (nginx). Nothing is actually written to the wire in
    /// this case; it exists so the event is logged as a cancellation, not as a server fault.
    /// </summary>
    private const int StatusClientClosedRequest = 499;

    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var effective = Unwrap(exception);
        var (statusCode, errorCode, title, extensions) = Describe(effective);

        var correlationId = httpContext.Request.Headers[Constants.HttpHeaderCorrelationIdKey].FirstOrDefault();
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        LogFailure(httpContext, effective, statusCode, errorCode, correlationId, traceId);

        // The client is gone — writing a body would throw again. Just record it.
        if (statusCode == StatusClientClosedRequest || httpContext.RequestAborted.IsCancellationRequested)
            return true;

        if (httpContext.Response.HasStarted)
        {
            logger.LogWarning(
                "Cannot write error response for {ErrorCode}: the response has already started. corr={CorrelationId}",
                errorCode, correlationId);

            return false;
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = ErrorTypeUrnPrefix + errorCode,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
        };

        problemDetails.Extensions["errorCode"] = errorCode;
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["traceId"] = traceId;

        foreach (var (key, value) in extensions)
            problemDetails.Extensions[key] = value;

        // Exception detail is a debugging aid only. Never expose it outside development —
        // it leaks types, paths and sometimes connection strings.
        if (IsDevelopmentLike())
        {
            problemDetails.Extensions["exceptionType"] = effective.GetType().FullName;
            problemDetails.Extensions["exceptionMessage"] = effective.Message;
            problemDetails.Extensions["stackTrace"] = effective.StackTrace;
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = effective,
        });
    }

    /// <summary>
    /// Maps an exception onto the response it should produce.
    /// </summary>
    /// <remarks>
    /// Anything implementing <see cref="IOxSException"/> is mapped from its own metadata, so
    /// services can add domain exceptions without touching this handler.
    /// </remarks>
    private (int StatusCode, string ErrorCode, string Title, IReadOnlyDictionary<string, object?> Extensions) Describe(
        Exception exception)
    {
        var none = (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>();

        switch (exception)
        {
            // Domain exceptions carry their own contract.
            case IOxSException oxs:
                return (oxs.StatusCode, oxs.ErrorCode, exception.Message, oxs.ProblemExtensions);

            // A downstream service failed. This is not our bug — say so, so that callers
            // (and on-call engineers) don't start debugging the wrong service.
            case InternalClientException internalClient:
                return (
                    StatusCodes.Status502BadGateway,
                    "upstream_service_error",
                    "A downstream service returned an unexpected response.",
                    new Dictionary<string, object?>
                    {
                        ["upstreamMethod"] = internalClient.Method,
                        ["upstreamEndpoint"] = internalClient.Endpoint,
                        ["upstreamStatus"] = (int?)internalClient.Result?.StatusCode,
                    });

            case PersistenceUnavailableException:
                return (
                    StatusCodes.Status503ServiceUnavailable,
                    "persistence_unavailable",
                    "The service is temporarily unable to reach its data store.",
                    none);

            // The caller went away (usually a closed browser tab). Not a fault.
            case OperationCanceledException:
                return (StatusClientClosedRequest, "client_closed_request", "The request was cancelled by the client.", none);

            // Genuinely unexpected. Title is deliberately generic — the detail goes to the log,
            // keyed by correlationId, not to the caller.
            default:
                return (
                    StatusCodes.Status500InternalServerError,
                    "unhandled",
                    "An unexpected error occurred while processing the request.",
                    none);
        }
    }

    /// <summary>
    /// Guards against pathological or self-referencing inner-exception chains.
    /// </summary>
    private const int MaxUnwrapDepth = 16;

    /// <summary>
    /// Resolves the exception that should determine the response.
    /// </summary>
    /// <remarks>
    /// Two passes:
    /// <list type="number">
    /// <item>
    /// Unwrap types annotated with <see cref="UnpackExceptionAttribute"/>, matching the behaviour
    /// of <see cref="CommonExceptionFilterAttribute{TException}"/>. A wrapper that is itself an
    /// <see cref="IOxSException"/> wins, so its own status code is not discarded.
    /// </item>
    /// <item>
    /// If that yields nothing meaningful, search the remaining inner chain for an
    /// <see cref="IOxSException"/>. Without this, a domain exception thrown inside a mapper or a
    /// <c>Task.WhenAll</c> surfaces as a 500 purely because something wrapped it.
    /// </item>
    /// </list>
    /// </remarks>
    private static Exception Unwrap(Exception exception)
    {
        var current = exception;

        for (var depth = 0; depth < MaxUnwrapDepth; depth++)
        {
            if (current is IOxSException)
                return current;

            if (current.GetType().GetCustomAttribute<UnpackExceptionAttribute>() is null
                || current.InnerException is null)
            {
                break;
            }

            current = current.InnerException;
        }

        // An AggregateException wrapping a single fault is effectively that fault.
        var candidate = exception is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions.FirstOrDefault() ?? current
            : current;

        var inner = candidate;

        for (var depth = 0; inner is not null && depth < MaxUnwrapDepth; depth++, inner = inner.InnerException)
        {
            if (inner is IOxSException)
                return inner;
        }

        return current;
    }

    /// <summary>
    /// Emits a single structured log entry containing every field needed to find this failure
    /// again: correlation id, trace id, route, status and error code.
    /// </summary>
    private void LogFailure(
        HttpContext httpContext,
        Exception exception,
        int statusCode,
        string errorCode,
        string? correlationId,
        string traceId)
    {
        // Client cancellations and 4xx are expected traffic, not incidents.
        var level = statusCode switch
        {
            StatusClientClosedRequest => LogLevel.Debug,
            >= 500 => LogLevel.Error,
            _ => LogLevel.Warning,
        };

        if (!logger.IsEnabled(level))
            return;

        var requestContext = httpContext.RequestServices.GetService(typeof(IRequestContext)) as IRequestContext;

        logger.Log(
            level,
            exception,
            "Request failed: {Method} {Path} -> {StatusCode} {ErrorCode} "
                + "corr={CorrelationId} trace={TraceId} user={UserId} org={OrganizationId}",
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            statusCode,
            errorCode,
            correlationId,
            traceId,
            requestContext?.UserId,
            requestContext?.OrganizationId);
    }

    private bool IsDevelopmentLike()
        => environment.IsDevelopment()
           || string.Equals(environment.EnvironmentName, "Local", StringComparison.OrdinalIgnoreCase);
}

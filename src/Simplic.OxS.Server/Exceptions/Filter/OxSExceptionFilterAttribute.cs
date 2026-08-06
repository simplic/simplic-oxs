using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using Simplic.OxS.Exceptions;

namespace Simplic.OxS.Server.Exceptions;

/// <summary>
/// Unified exception filter for every <see cref="OxSException"/>.
/// <para>
/// Reads the HTTP response metadata carried by the exception (status code, whether to emit
/// problem details, title, type and any extension members) and builds the matching response —
/// an RFC 7807 <c>ProblemDetails</c> body by default, or the plain message when the exception
/// opts out.
/// </para>
/// </summary>
public class OxSExceptionFilterAttribute : CommonExceptionFilterAttribute<OxSException>
{
    /// <inheritdoc/>
    protected override void HandleException(ExceptionContext context, OxSException exception)
    {
        if (!exception.IncludeProblemDetails)
        {
            context.Result = new ObjectResult(exception.Message)
            {
                StatusCode = exception.StatusCode
            };

            return;
        }

        var problemDetails = new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.Title ?? ReasonPhrases.GetReasonPhrase(exception.StatusCode),
            Detail = exception.Message,
            Type = exception.ProblemType ?? "about:blank",
            Instance = context.HttpContext.Request.Path
        };

        exception.PopulateProblemDetails(problemDetails.Extensions);

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = exception.StatusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}

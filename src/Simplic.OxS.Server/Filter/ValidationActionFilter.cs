using Microsoft.AspNetCore.Mvc.Filters;
using Simplic.OxS.Exceptions;

namespace Simplic.OxS.Server.Filter
{
    /// <summary>
    /// Turns model-state (data-annotation / binding) failures into the same RFC 9457
    /// <c>application/problem+json</c> contract as a thrown <see cref="BadRequestException"/>, so a
    /// client only ever sees one 400 body shape for a given endpoint.
    /// <para>
    /// It builds a <see cref="BadRequestException"/> from the invalid model state and throws it; the
    /// global exception-handler chain renders the response. The built-in <c>[ApiController]</c>
    /// model-state filter is suppressed in <c>Bootstrap</c> so this filter owns the contract for every
    /// controller.
    /// </para>
    /// </summary>
    public class ValidationActionFilter : IActionFilter
    {
        /// <inheritdoc />
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState.IsValid)
                return;

            var errors = context.ModelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                            ? "The value is invalid."
                            : error.ErrorMessage)
                        .ToArray());

            throw new BadRequestException(errors);
        }

        /// <inheritdoc />
        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
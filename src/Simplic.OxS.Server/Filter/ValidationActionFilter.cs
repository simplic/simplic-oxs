using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    /// <para>
    /// Whatever answers an invalid model state ahead of this filter does so through
    /// <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c>, which <c>Bootstrap</c> sets to
    /// <see cref="Answer"/>: the same document, written without the throw.
    /// </para>
    /// </summary>
    public class ValidationActionFilter : IActionFilter
    {
        /// <inheritdoc />
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ModelState.IsValid)
                return;

            throw Refusal(context.ModelState);
        }

        /// <inheritdoc />
        public void OnActionExecuted(ActionExecutedContext context)
        {
        }

        /// <summary>
        /// The result that writes the host's answer to the invalid model state of
        /// <paramref name="context"/>: the problem document of the exception this filter throws.
        /// </summary>
        internal static IActionResult Answer(ActionContext context) => new InvalidModelStateResult(Refusal(context.ModelState));

        /// <summary>The exception that carries the errors of <paramref name="modelState"/>, one list of sentences per place.</summary>
        private static BadRequestException Refusal(ModelStateDictionary modelState)
        {
            var errors = modelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                            ? "The value is invalid."
                            : error.ErrorMessage)
                        .ToArray());

            return new BadRequestException(errors);
        }
    }
}
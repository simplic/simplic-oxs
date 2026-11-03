using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Exceptions;
using Simplic.OxS.Server.Exceptions;

namespace Simplic.OxS.Server.Filter
{
    /// <summary>
    /// Writes the problem document of <paramref name="refusal"/> as the exception-handler chain
    /// writes it for the same exception thrown, through the one writer both use.
    /// </summary>
    internal sealed class InvalidModelStateResult(OxSException refusal) : IActionResult
    {
        /// <inheritdoc />
        public Task ExecuteResultAsync(ActionContext context) =>
            ProblemDetailsResponseWriter.WriteAsync(context.HttpContext, refusal, context.HttpContext.RequestAborted).AsTask();
    }
}

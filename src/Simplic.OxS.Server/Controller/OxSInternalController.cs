using Microsoft.AspNetCore.Mvc;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Base controller for internal-only endpoints, gated by the internal API key.
    /// </summary>
    /// <remarks>
    /// <see cref="ApiControllerAttribute"/> is applied here for the same reasons as on
    /// <see cref="OxSController"/> — automatic <c>400 ProblemDetails</c>, binding-source
    /// inference and attribute-routing enforcement.
    /// </remarks>
    [ApiController]
    [AuthorizeInternalApiKey]
    public abstract class OxSInternalController : ControllerBase
    {
        /// <summary>
        /// Get raw json from http context (context.Items key ~rawJson)
        /// </summary>
        /// <returns>Raw json (nullable)</returns>
        protected string? GetRawJson() =>
            HttpContext.Items.FirstOrDefault(x => x.Key?.ToString() == "rawJson").Value?.ToString();
    }
}

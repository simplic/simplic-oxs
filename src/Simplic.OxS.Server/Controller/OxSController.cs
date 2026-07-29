using Microsoft.AspNetCore.Mvc;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Represents the simplic oxs base controller
    /// </summary>
    /// <remarks>
    /// <see cref="ApiControllerAttribute"/> is applied here so every derived controller gets
    /// automatic <c>400 ProblemDetails</c> for invalid models, binding-source inference and
    /// attribute-routing enforcement without repeating the attribute. Derived controllers may
    /// still declare <c>[ApiController]</c> themselves — it is idempotent.
    /// <para>
    /// Because <c>[ApiController]</c> already rejects invalid models, derived controllers must not
    /// write <c>if (!ModelState.IsValid) return BadRequest();</c> — it is unreachable.
    /// </para>
    /// </remarks>
    [ApiController]
    public abstract class OxSController : ControllerBase
    {
        /// <summary>
        /// Get raw json from http context (context.Items key ~rawJson)
        /// </summary>
        /// <returns>Raw json (nullable)</returns>
        protected string? GetRawJson() =>
            HttpContext.Items.FirstOrDefault(x => x.Key?.ToString() == "rawJson").Value?.ToString();
    }
}

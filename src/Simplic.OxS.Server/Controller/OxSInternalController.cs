using Microsoft.AspNetCore.Mvc;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Represents the simplic oxs base controller
    /// </summary>
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

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Represents the simplic oxs base controller
    /// </summary>
    public abstract class OxSController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        /// <summary>
        /// Get raw json from http context (context.Items key ~rawJson)
        /// </summary>
        /// <returns>Raw json (nullable)</returns>
        public string? GetRawJson() =>
            HttpContext.Items.FirstOrDefault(x => x.Key?.ToString() == "rawJson").Value?.ToString();
    }
}

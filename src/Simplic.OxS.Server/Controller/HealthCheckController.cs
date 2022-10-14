using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Controller for supporting k8s health checks
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("/[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class HealthCheckController : ControllerBase
    {
        /// <summary>
        /// Return status code 200
        /// </summary>
        /// <returns>Status code 200</returns>
        public ActionResult Get() => Ok();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Serves the legacy model definition document under <c>GET /ModelDefinition</c>: the same
    /// frozen bytes for every caller, built once at startup. Nothing is folded in per request.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("/ModelDefinition")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class ModelDefinitionController(OxSchemaRegistry registry) : OxSController
    {
        /// <summary>The model definition document, or 404 when the host declares no controllers.</summary>
        [HttpGet]
        public IActionResult Get(CancellationToken ct)
        {
            if (registry.ModelDefinition is not { } document)
                return NotFound();

            return File(document.Body, "application/json");
        }
    }
}

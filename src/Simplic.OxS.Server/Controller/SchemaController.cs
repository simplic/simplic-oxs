using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Serves the schema document under <c>GET /schema</c>. Anonymous, because the document is
    /// organisation-independent; hidden from the API explorer so no service's swagger moves.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("/schema")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class SchemaController(OxSchemaRegistry registry) : OxSController
    {
        /// <summary>The schema document, or 304 when <c>If-None-Match</c> names its revision.</summary>
        [HttpGet]
        public IActionResult Get(CancellationToken ct)
        {
            var tag = new EntityTagHeaderValue(registry.ETag);

            Response.Headers.CacheControl = "private, must-revalidate";
            Response.Headers.ETag = registry.ETag;

            // Weak comparison, as If-None-Match requires: a proxy may weaken the tag and it must still match.
            if (Request.GetTypedHeaders().IfNoneMatch.Any(candidate => candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(tag, useStrongComparison: false)))
                return StatusCode(StatusCodes.Status304NotModified);

            return File(registry.Body, "application/json");
        }
    }
}

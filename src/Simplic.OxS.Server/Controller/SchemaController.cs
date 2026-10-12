using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using OxQL.Model.Addon;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Serves the schema document under <c>GET /schema</c> and the organisation's addon
    /// definitions under <c>GET /schema/addons</c>. The document is organisation-independent
    /// and anonymous unless the host requires authorization for it
    /// (<see cref="OxSchemaBuildOptions.RequireAuthorization"/>); the definitions are per
    /// organisation and need the caller's. Both routes are hidden from the API explorer: the
    /// document is its own description and no generated client is meant to call it.
    /// <para>
    /// Both bodies are written in the content coding the caller accepts (<c>Accept-Encoding</c>:
    /// Brotli, else gzip; none without the header), the document from a body coded once.
    /// </para>
    /// </summary>
    [ApiController]
    [Route("/schema")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class SchemaController(OxSchemaRegistry registry, IAddonDefinitionSource addons, IRequestContext requestContext) : OxSController
    {
        /// <summary>The schema document, or 304 when <c>If-None-Match</c> names its revision.</summary>
        [HttpGet]
        [AllowAnonymous]
        [OxSchemaAuthorization]
        public IActionResult Get(CancellationToken ct) =>
            Serve(registry.Body, registry.ETag, registry.Coded);

        /// <summary>
        /// The organisation's addon definitions per extendable entity, each list in the schema's
        /// descriptor format, or 304 when <c>If-None-Match</c> names the current list.
        /// </summary>
        [HttpGet("addons")]
        [Authorize]
        public async Task<IActionResult> GetAddonsAsync(CancellationToken ct)
        {
            if (requestContext.OrganizationId is not { } organisation)
                return StatusCode(StatusCodes.Status403Forbidden);

            var result = await AddonDescriptors.BuildAsync(registry.Model, addons, organisation, ct);

            return Serve(result.Body, result.ETag);
        }

        private IActionResult Serve(byte[] body, string etag, Func<string, byte[]>? coded = null)
        {
            var tag = new EntityTagHeaderValue(etag);

            Response.Headers.CacheControl = "private, must-revalidate";
            Response.Headers.ETag = etag;

            // Weak comparison, as If-None-Match requires: a proxy may weaken the tag and it must still match.
            if (Request.GetTypedHeaders().IfNoneMatch.Any(candidate => candidate.Equals(EntityTagHeaderValue.Any) || candidate.Compare(tag, useStrongComparison: false)))
                return StatusCode(StatusCodes.Status304NotModified);

            return new CodedJson(body, coded);
        }

        /// <summary>
        /// A finished JSON body, written in the content coding the caller accepts
        /// (<c>Accept-Encoding</c>: Brotli, else gzip; as it is without the header or below a kilobyte).
        /// </summary>
        private sealed class CodedJson(byte[] body, Func<string, byte[]>? coded) : FileContentResult(body, "application/json")
        {
            public override Task ExecuteResultAsync(ActionContext context)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;

                return global::OxQL.AspNetCore.Models.WireCompression.WriteJsonAsync(context.HttpContext.Response, FileContents, coded);
            }
        }
    }
}

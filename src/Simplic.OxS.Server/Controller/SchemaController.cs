using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using OxQL.Model.Addon;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Serves the schema document under <c>GET /schema</c> and the organisation's addon
    /// definitions under <c>GET /schema/addons</c>. The document is organisation-independent
    /// and anonymous unless the host requires authorization for it
    /// (<see cref="OxSchemaBuildOptions.RequireAuthorization"/>); the definitions are per
    /// organisation and need the caller's.
    /// <para>
    /// Both bodies are written in the content coding the caller accepts (<c>Accept-Encoding</c>:
    /// Brotli, else gzip; none without the header), the document from a body coded once.
    /// </para>
    /// <para>
    /// What the OpenAPI document shows of a route is what its attributes say: the summary, the
    /// description, every status it answers (<see cref="DescribedRouteAttribute"/>). The format
    /// of the document is described in one place, which the descriptions point to.
    /// </para>
    /// <para>
    /// The document needs no database, so the controller asks for the addon definitions only
    /// where they are served. A host that is configured with none (no <c>MongoDB</c> section)
    /// serves the document and answers the definitions 404 with a problem that says so.
    /// </para>
    /// </summary>
    [ApiController]
    [Route("/schema")]
    public sealed class SchemaController : OxSController
    {
        /// <summary>What the problem of <c>GET /schema/addons</c> says on a host that is configured with no database.</summary>
        internal const string NoDatabase = "This service holds no addon definitions: it is configured with no database (the MongoDB section).";

        private readonly OxSchemaRegistry registry;
        private readonly IAddonDefinitionSource? addons;
        private readonly IRequestContext requestContext;

        /// <summary>The controller as a host creates it: the addon definitions are asked of the request's services, by the route that serves them.</summary>
        /// <param name="registry">The schema of the host.</param>
        /// <param name="requestContext">The request's context.</param>
        [ActivatorUtilitiesConstructor]
        public SchemaController(OxSchemaRegistry registry, IRequestContext requestContext)
        {
            this.registry = registry;
            this.requestContext = requestContext;
        }

        /// <summary>The controller over a given source of addon definitions.</summary>
        /// <param name="registry">The schema of the host.</param>
        /// <param name="addons">The addon definitions <c>GET /schema/addons</c> serves.</param>
        /// <param name="requestContext">The request's context.</param>
        public SchemaController(OxSchemaRegistry registry, IAddonDefinitionSource addons, IRequestContext requestContext)
            : this(registry, requestContext)
        {
            this.addons = addons;
        }

        /// <summary>The address of the description of the schema document's format.</summary>
        internal const string Documentation = "https://github.com/simplic/simplic-oxs/blob/master/OX_SCHEMA.md";

        private const string Json = "application/json";

        private const string ProblemJson = "application/problem+json";

        /// <summary>The Ox schema document of this service.</summary>
        /// <param name="ct">Ends the request when the caller gives up.</param>
        [HttpGet]
        [AllowAnonymous]
        [OxSchemaAuthorization]
        [EndpointSummary("The Ox schema document of this service.")]
        [EndpointDescription(
            "Describes the entities of this service a query can read: their types, keys, references and REST operations, and the limits a query is checked against. "
            + "The document is the same for every organisation and does not change while the service runs; `revision` and the `ETag` name its content. "
            + "Anonymous unless the service requires authorization for it. The format is described in " + Documentation + ".")]
        [DescribedRoute(Conditional = true, AnswerExample = DescribedRouteExamples.Schema)]
        [ProducesResponseType(typeof(OxSchemaDocument), StatusCodes.Status200OK, Json, Description = "The document.")]
        [ProducesResponseType(StatusCodes.Status304NotModified, Description = "The document the caller holds (`If-None-Match`) is the current one.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, ProblemJson, Description = "The service requires authorization for the document and the request carries no credentials.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, ProblemJson, Description = "The service requires authorization for the document and the caller may not read it.")]
        public IActionResult Get(CancellationToken ct) =>
            Serve(registry.Body, registry.ETag, registry.Coded);

        /// <summary>The addon definitions of the caller's organisation, per entity that takes them.</summary>
        /// <param name="ct">Ends the request when the caller gives up.</param>
        [HttpGet("addons")]
        [Authorize]
        [EndpointSummary("The addon definitions of the caller's organisation, per entity that takes them.")]
        [EndpointDescription(
            "Answers one list per entity of this service that takes addon definitions, keyed by entity id: an entity that is declared extendable and has an `addon` member. "
            + "A list holds the organisation's addon definitions as property descriptors in the schema document's format, "
            + "which are the properties of the entity's `addon` member. An entity without definitions has an empty list. The format is described in " + Documentation + ".")]
        [DescribedRoute(Conditional = true, AnswerExample = DescribedRouteExamples.SchemaAddons)]
        [ProducesResponseType(typeof(IDictionary<string, IReadOnlyList<OxSchemaProperty>>), StatusCodes.Status200OK, Json, Description = "The definitions per entity id.")]
        [ProducesResponseType(StatusCodes.Status304NotModified, Description = "The lists the caller holds (`If-None-Match`) are the current ones.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, ProblemJson, Description = "The request carries no credentials.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, ProblemJson, Description = "The request belongs to no organisation.")]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, ProblemJson, Description = "The service is configured with no database, so it holds no addon definitions.")]
        public async Task<IActionResult> GetAddonsAsync(CancellationToken ct)
        {
            if (requestContext.OrganizationId is not { } organisation)
                return StatusCode(StatusCodes.Status403Forbidden);

            if (AddonDefinitions() is not { } source)
                return Problem(detail: NoDatabase, statusCode: StatusCodes.Status404NotFound, title: "Not Found");

            var result = await AddonDescriptors.BuildAsync(registry.Model, source, organisation, ct);

            return Serve(result.Body, result.ETag);
        }

        /// <summary>
        /// The source of the addon definitions, or null on a host that is configured with no
        /// database: there the host registers <see cref="NoDatabaseAddonDefinitionSource"/>.
        /// </summary>
        private IAddonDefinitionSource? AddonDefinitions() =>
            (addons ?? HttpContext.RequestServices.GetRequiredService<IAddonDefinitionSource>()) is var source and not NoDatabaseAddonDefinitionSource
                ? source
                : null;

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

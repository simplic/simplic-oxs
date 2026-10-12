using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Simplic.OxS.Server.OxSchema;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// What the OpenAPI document says of a route of this package beyond what the route's own
    /// attributes say (<see cref="DescribedRoute"/>): no security requirement on a route that
    /// admits anonymous callers, the headers of a conditional request, and one example of the
    /// request and of the answer. It touches no other operation, so the document of a service's
    /// own controllers is the one it was.
    /// </summary>
    internal sealed class DescribedRouteOperationFilter(IServiceProvider services) : IOperationFilter
    {
        private const string Json = "application/json";

        /// <inheritdoc/>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (DescribedRoute.Of(context.ApiDescription) is not { } route)
                return;

            if (IsAnonymous(context.ApiDescription))
            {
                // One empty requirement: the route is called without credentials, whatever the document requires elsewhere.
                operation.Security = [new OpenApiSecurityRequirement()];

                // What only a caller with missing or refused credentials is answered.
                operation.Responses?.Remove(StatusCodes.Status401Unauthorized.ToString());
                operation.Responses?.Remove(StatusCodes.Status403Forbidden.ToString());
            }

            if (route.Conditional)
                DescribeConditional(operation);

            if (route.RequestExample is { } request && operation.RequestBody?.Content is { } bodies && bodies.TryGetValue(Json, out var body))
                body.Example = JsonNode.Parse(request);

            if (route.AnswerExample is { } answer
                && operation.Responses is { } responses
                && responses.TryGetValue(StatusCodes.Status200OK.ToString(), out var ok)
                && ok.Content is { } answers
                && answers.TryGetValue(Json, out var written))
            {
                written.Example = JsonNode.Parse(answer);
            }
        }

        /// <summary>
        /// Whether the route is called without credentials on this host. The schema document's
        /// route is anonymous unless the host requires authorization for it
        /// (<see cref="OxSchemaBuildOptions.RequireAuthorization"/>).
        /// </summary>
        private bool IsAnonymous(ApiDescription description)
        {
            var metadata = description.ActionDescriptor.EndpointMetadata;

            if (!metadata.OfType<IAllowAnonymous>().Any())
                return false;

            return !metadata.OfType<OxSchemaAuthorizationAttribute>().Any()
                || services.GetService<OxSchemaRegistry>() is not { RequireAuthorization: true };
        }

        /// <summary>The validator a caller sends back, and the header the answers carry it in.</summary>
        private static void DescribeConditional(OpenApiOperation operation)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-None-Match",
                In = ParameterLocation.Header,
                Required = false,
                Description = "The `ETag` of the answer the caller holds. The route answers 304 without a body while that answer still stands.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });

            foreach (var status in new[] { StatusCodes.Status200OK, StatusCodes.Status304NotModified })
            {
                if (operation.Responses is null || !operation.Responses.TryGetValue(status.ToString(), out var found) || found is not OpenApiResponse response)
                    continue;

                response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                response.Headers["ETag"] = new OpenApiHeader
                {
                    Description = "The validator of the answer, to send back as `If-None-Match`.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                };
            }
        }
    }

    /// <summary>What <see cref="DescribedRouteOperationFilter"/> adds to one route.</summary>
    /// <param name="Conditional">Whether the route answers 304 to a matching <c>If-None-Match</c>.</param>
    /// <param name="RequestExample">An example of the request body as JSON text, or null.</param>
    /// <param name="AnswerExample">An example of the 200 answer as JSON text, or null.</param>
    internal sealed record DescribedRoute(bool Conditional, string? RequestExample, string? AnswerExample)
    {
        /// <summary>
        /// What the route behind <paramref name="description"/> is described by, or null for a
        /// route that is none of this package's described ones.
        /// </summary>
        public static DescribedRoute? Of(ApiDescription description)
        {
            ArgumentNullException.ThrowIfNull(description);

            return description.ActionDescriptor.EndpointMetadata.OfType<DescribedRouteAttribute>().LastOrDefault() is { } declared
                ? new DescribedRoute(declared.Conditional, null, declared.AnswerExample)
                : null;
        }
    }
}

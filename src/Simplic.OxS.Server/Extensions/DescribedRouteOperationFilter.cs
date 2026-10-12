using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using OxQL.AspNetCore;
using OxQL.AspNetCore.OpenApi;
using Simplic.OxS.Server.OxSchema;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// What the OpenAPI document says of a route of this package beyond what the route's own
    /// attributes say (<see cref="DescribedRoute"/>): no security requirement on a route that
    /// admits anonymous callers, the headers of a conditional request, and one example of the
    /// request and of the answer. Of a route of the query engine it also says that the body is
    /// JSON, which values the contract header takes, that what the host answers before the engine
    /// sees a request is a problem document, and where the bodies of contract 1 are described. It
    /// touches no other operation, so the document of a service's own controllers is the one it was.
    /// </summary>
    internal sealed class DescribedRouteOperationFilter(IServiceProvider services) : IOperationFilter
    {
        private const string Json = "application/json";

        private const string ProblemJson = "application/problem+json";

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

            if (route.Query)
                DescribeQueryRoute(operation, context, route);

            if (route.Conditional)
                DescribeConditional(operation);

            if (route.RequestExample is { } request && operation.RequestBody?.Content is { } bodies && bodies.TryGetValue(Json, out var body))
                body.Example = JsonNode.Parse(request);

            Example(operation, StatusCodes.Status200OK, route.AnswerExample);
            Example(operation, StatusCodes.Status400BadRequest, route.RefusalExample);
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

        private static void Example(OpenApiOperation operation, int status, string? example)
        {
            if (example is not null
                && operation.Responses is { } responses
                && responses.TryGetValue(status.ToString(), out var response)
                && response.Content is { } content
                && content.TryGetValue(Json, out var written))
            {
                written.Example = JsonNode.Parse(example);
            }
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

        /// <summary>What holds for every route of the query engine.</summary>
        private static void DescribeQueryRoute(OpenApiOperation operation, OperationFilterContext context, DescribedRoute route)
        {
            // The body is JSON. The host's formatter also reads the other JSON media types; a client is generated for the one.
            if (operation.RequestBody?.Content is { } bodies)
                foreach (var other in bodies.Keys.Where(media => media != Json).ToList())
                    bodies.Remove(other);

            // The two contracts are the header's values; any other is refused.
            if (operation.Parameters?.FirstOrDefault(parameter => parameter.In == ParameterLocation.Header && parameter.Name == OxQLQueryService.ContractHeader) is OpenApiParameter { Schema: OpenApiSchema header })
                header.Enum = [JsonValue.Create("1"), JsonValue.Create("2")];

            // What the host answers before the engine sees the request (no credentials, a route switched off) is its problem document.
            var problem = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository);

            foreach (var response in (operation.Responses ?? []).Values.OfType<OpenApiResponse>())
                if (response.Content is { Count: > 0 } content && content.Values.All(media => media.Schema is OpenApiSchemaReference { Reference.Id: nameof(ProblemDetails) }))
                    response.Content = new Dictionary<string, OpenApiMediaType> { [ProblemJson] = new OpenApiMediaType { Schema = problem } };

            // A caller that is refused for sending too much is told when to come back.
            if (operation.Responses is { } answers && answers.TryGetValue(StatusCodes.Status429TooManyRequests.ToString(), out var limited) && limited is OpenApiResponse tooMany)
            {
                tooMany.Headers ??= new Dictionary<string, IOpenApiHeader>();
                tooMany.Headers["Retry-After"] = new OpenApiHeader
                {
                    Description = "The seconds after which the request may be sent again.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Integer },
                };
            }

            if (route.LegacyBody is null && route.LegacyAnswer is null)
                return;

            // Contract 1 is named, not offered: its models are in the document for a caller that still writes it,
            // marked deprecated, and the operation stays the one a client of the current contract is generated from.
            var legacy = new[] { route.LegacyBody, route.LegacyAnswer }.Where(model => model is not null).Select(model => Publish(model!, context)).ToList();

            operation.Description += $" Contract 1 is not what this operation describes: its body and answer are the deprecated schemas {string.Join(" and ", legacy.Select(name => $"`{name}`"))}.";
        }

        /// <summary>Puts a contract 1 model and the models it uses among the document's schemas, each marked deprecated; answers its name.</summary>
        private static string Publish(Type model, OperationFilterContext context)
        {
            var before = context.SchemaRepository.Schemas.Keys.ToHashSet(StringComparer.Ordinal);
            var name = context.SchemaGenerator.GenerateSchema(model, context.SchemaRepository) is OpenApiSchemaReference { Reference.Id: { } id } ? id : model.Name;

            foreach (var (added, schema) in context.SchemaRepository.Schemas)
                if (!before.Contains(added) && schema is OpenApiSchema published)
                    published.Deprecated = true;

            return name;
        }
    }

    /// <summary>What <see cref="DescribedRouteOperationFilter"/> adds to one route.</summary>
    /// <param name="Conditional">Whether the route answers 304 to a matching <c>If-None-Match</c>.</param>
    /// <param name="RequestExample">An example of the request body as JSON text, or null.</param>
    /// <param name="AnswerExample">An example of the 200 answer as JSON text, or null.</param>
    internal sealed record DescribedRoute(bool Conditional, string? RequestExample, string? AnswerExample)
    {
        /// <summary>Whether the route is one of the query engine's.</summary>
        public bool Query { get; init; }

        /// <summary>An example of the 400 answer as JSON text, or null.</summary>
        public string? RefusalExample { get; init; }

        /// <summary>The model of the body a contract 1 caller writes, or null when the route has none of its own.</summary>
        public Type? LegacyBody { get; init; }

        /// <summary>The model of the answer a contract 1 caller reads, or null when it is the route's answer.</summary>
        public Type? LegacyAnswer { get; init; }

        /// <summary>
        /// What the route behind <paramref name="description"/> is described by, or null for a
        /// route that is none of this package's described ones: an action of this package that
        /// carries <see cref="DescribedRouteAttribute"/>, or a route of the query engine, which says
        /// what it is by its <see cref="OxQLOperationAttribute"/>.
        /// </summary>
        public static DescribedRoute? Of(ApiDescription description)
        {
            ArgumentNullException.ThrowIfNull(description);

            var metadata = description.ActionDescriptor.EndpointMetadata;

            if (metadata.OfType<DescribedRouteAttribute>().LastOrDefault() is { } declared)
                return new DescribedRoute(declared.Conditional, null, declared.AnswerExample);

            if (metadata.OfType<OxQLOperationAttribute>().LastOrDefault() is not { } query)
                return null;

            // A route that answers 304 says so; the engine's explain does.
            var conditional = description.SupportedResponseTypes.Any(response => response.StatusCode == StatusCodes.Status304NotModified);
            var (request, answer) = DescribedRouteExamples.OfQueryRoute(query.Body);

            return new DescribedRoute(conditional, request, answer)
            {
                Query = true,
                RefusalExample = query.Body is null ? null : DescribedRouteExamples.Refusal,
                LegacyBody = query.Contract1Body,
                LegacyAnswer = query.Contract1Answer,
            };
        }
    }
}

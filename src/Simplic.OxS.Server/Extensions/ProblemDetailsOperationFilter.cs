using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// Declares the standard error responses (RFC 9457 <c>application/problem+json</c>) on every
    /// operation in the OpenAPI document, so generated clients (e.g. Orval) get a typed error contract
    /// instead of <c>unknown</c>. Controllers can still override any status with an explicit
    /// <c>[ProducesResponseType]</c> — existing entries are left untouched. A route of this package
    /// that declares every status it answers (<see cref="DescribedRoute"/>) gets none added.
    /// </summary>
    internal sealed class ProblemDetailsOperationFilter : IOperationFilter
    {
        private static readonly (string Code, string Description)[] ErrorResponses =
        {
            ("400", "Bad Request"),
            ("401", "Unauthorized"),
            ("403", "Forbidden"),
            ("404", "Not Found"),
            ("409", "Conflict"),
            ("422", "Unprocessable Content"),
            ("500", "Internal Server Error"),
        };

        /// <inheritdoc/>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (DescribedRoute.Of(context.ApiDescription) is not null)
                return;

            var schema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository);

            foreach (var (code, description) in ErrorResponses)
            {
                if (operation.Responses.ContainsKey(code))
                    continue;

                operation.Responses[code] = new OpenApiResponse
                {
                    Description = description,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new OpenApiMediaType { Schema = schema }
                    }
                };
            }
        }
    }
}

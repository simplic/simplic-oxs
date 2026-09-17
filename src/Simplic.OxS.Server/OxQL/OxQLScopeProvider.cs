using Microsoft.AspNetCore.Http;
using OxQL.AspNetCore.Scope;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// Tells the query engine which organisation a request belongs to: the request context's,
/// filled by <see cref="Filter.RequestContextActionFilter"/> from the bearer token or, on an
/// internal call, from the forwarded headers. The engine applies <c>organizationId eq</c> at
/// every entry into an entity and answers 403 when the context carries no organisation.
/// </summary>
public sealed class OxQLScopeProvider(IRequestContext requestContext) : IOxQLScopeProvider
{
    /// <inheritdoc/>
    public ValueTask<Guid?> OrganisationAsync(HttpContext? httpContext, CancellationToken cancellationToken) =>
        ValueTask.FromResult(requestContext.OrganizationId);

    /// <inheritdoc/>
    public string? UserId(HttpContext? httpContext) => requestContext.UserId?.ToString();

    /// <inheritdoc/>
    public string? CorrelationId(HttpContext? httpContext) => requestContext.CorrelationId?.ToString() ?? httpContext?.TraceIdentifier;
}

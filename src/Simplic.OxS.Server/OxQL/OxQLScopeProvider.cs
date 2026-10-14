using Microsoft.AspNetCore.Http;
using OxQL.AspNetCore.Scope;
using Simplic.OxS.Server.Filter;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// Tells the query engine which organisation a request belongs to: the request context's,
/// filled by <see cref="RequestContextActionFilter"/> from the bearer token or, on an
/// internal call whose key was checked, from the forwarded headers. The engine applies <c>organizationId eq</c> at
/// every entry into an entity and answers 403 when the context carries no organisation.
/// <para>
/// The engine also asks before the action runs, when the context is not filled yet: for the
/// caller an explain is counted against, and for what it logs about a request it answers before
/// the action. The provider then reads the request by the rules the filter applies
/// (<see cref="RequestIdentity"/>): the claims of the authenticated principal, and the
/// forwarded headers of an internal call only once its key was checked. A request that names
/// nobody by those rules has neither user nor organisation here.
/// </para>
/// </summary>
public sealed class OxQLScopeProvider(IRequestContext requestContext) : IOxQLScopeProvider
{
    /// <inheritdoc/>
    public ValueTask<Guid?> OrganisationAsync(HttpContext? httpContext, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Identity(httpContext).OrganizationId);

    /// <inheritdoc/>
    public string? UserId(HttpContext? httpContext) => Identity(httpContext).UserId?.ToString();

    /// <inheritdoc/>
    public string? CorrelationId(HttpContext? httpContext)
    {
        var correlation = requestContext.CorrelationId
            ?? (httpContext is null ? null : RequestContextActionFilter.GetFromHeader(httpContext, Constants.HttpHeaderCorrelationIdKey));

        return correlation?.ToString() ?? httpContext?.TraceIdentifier;
    }

    /// <summary>
    /// The request context's user and organisation where it holds either; else what the request
    /// says by the filter's rules, which is what the filter fills the context with.
    /// </summary>
    private (Guid? UserId, Guid? OrganizationId) Identity(HttpContext? httpContext)
    {
        if (httpContext is null || requestContext.UserId is not null || requestContext.OrganizationId is not null)
            return (requestContext.UserId, requestContext.OrganizationId);

        return RequestIdentity.Read(httpContext, forwardedHeaders: RequestIdentity.InternalKeyAdmitted(httpContext)) ?? default;
    }
}

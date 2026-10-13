using Microsoft.AspNetCore.Http;

namespace Simplic.OxS.Server.Filter
{
    /// <summary>
    /// Who a request is made by: the one definition of the rules, for the request context
    /// (<see cref="RequestContextActionFilter"/>) and for whoever asks before the context is
    /// filled (<see cref="OxQL.OxQLScopeProvider"/>).
    /// <list type="bullet">
    /// <item>A bearer token (<c>bearer</c>, <c>v-bearer</c>, <c>x-bearer</c>): the <c>Id</c> and <c>OId</c> claims of the authenticated principal.</item>
    /// <item>The internal key scheme: the forwarded <c>UserId</c> and <c>OrganizationId</c> headers.</item>
    /// <item>An api key (<c>x-api-key</c>): the claims of the authenticated principal, whatever the rules above read.</item>
    /// </list>
    /// </summary>
    internal static class RequestIdentity
    {
        /// <summary>
        /// The <see cref="HttpContext.Items"/> key under which <see cref="AuthorizeInternalApiKeyAttribute"/>
        /// notes that the request presented the host's internal key.
        /// </summary>
        internal const string InternalKeyAdmittedItem = "Simplic.OxS.InternalKeyAdmitted";

        /// <summary>
        /// Reads the user and the organisation of a request, or null where none of the rules
        /// applies to it. With <paramref name="forwardedHeaders"/> false the headers of the
        /// internal key scheme are not read: they say who the request is made for only once
        /// the key was checked.
        /// </summary>
        internal static (Guid? UserId, Guid? OrganizationId)? Read(HttpContext httpContext, bool forwardedHeaders)
        {
            (Guid? UserId, Guid? OrganizationId)? identity = null;

            var scheme = httpContext.Request.Headers.Authorization.ToString().Split(" ")[0].ToLower();

            if (scheme is Constants.HttpAuthorizationSchemeBearerKey or "v-bearer" or "x-bearer")
                identity = FromClaims(httpContext);

            if (scheme == Constants.HttpAuthorizationSchemeInternalKey && forwardedHeaders)
            {
                identity = (
                    RequestContextActionFilter.GetFromHeader(httpContext, Constants.HttpHeaderUserIdKey),
                    RequestContextActionFilter.GetFromHeader(httpContext, Constants.HttpHeaderOrganizationIdKey));
            }

            if (httpContext.Request.Headers.TryGetValue(Constants.HttpHeaderApiKey, out var apiKey) && !string.IsNullOrEmpty(apiKey))
                identity = FromClaims(httpContext);

            return identity;
        }

        /// <summary>Whether the request presented the host's internal key and the key was checked.</summary>
        internal static bool InternalKeyAdmitted(HttpContext httpContext) => httpContext.Items.ContainsKey(InternalKeyAdmittedItem);

        private static (Guid? UserId, Guid? OrganizationId) FromClaims(HttpContext httpContext) =>
            (Claim(httpContext, "Id"), Claim(httpContext, "OId"));

        private static Guid? Claim(HttpContext httpContext, string type)
        {
            var claim = httpContext.User.Claims.FirstOrDefault(x => x.Type == type);

            return !string.IsNullOrWhiteSpace(claim?.Value) && Guid.TryParse(claim.Value, out var result) ? result : null;
        }
    }
}

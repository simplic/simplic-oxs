using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace Simplic.OxS.Server.Filter
{
    /// <summary>
    /// Middleware for retrieving request context data
    /// </summary>
    internal class RequestContextActionFilter : IActionFilter
    {
        private readonly IRequestContext requestContext;

        /// <summary>
        /// Initialize filter
        /// </summary>
        /// <param name="requestContext">Scoped request context</param>
        public RequestContextActionFilter(IRequestContext requestContext)
        {
            this.requestContext = requestContext;
        }

        /// <summary>
        /// Create request context: the correlation id of the request's header, and the user and
        /// the organisation by the rules of <see cref="RequestIdentity"/>. The forwarded headers
        /// of the internal key scheme are read only where the key was checked: on a controller
        /// behind <see cref="AuthorizeInternalApiKeyAttribute"/>, whose filter runs before this one.
        /// </summary>
        /// <param name="executionContext"></param>
        public void OnActionExecuting(ActionExecutingContext executionContext)
        {
            requestContext.CorrelationId = GetFromHeader(executionContext.HttpContext, Constants.HttpHeaderCorrelationIdKey);

            if (RequestIdentity.Read(executionContext.HttpContext, RequestIdentity.InternalKeyAdmitted(executionContext.HttpContext)) is { } identity)
            {
                requestContext.UserId = identity.UserId;
                requestContext.OrganizationId = identity.OrganizationId;
            }
        }

        /// <summary>
        /// Request ended
        /// </summary>
        /// <param name="context"></param>
        public void OnActionExecuted(ActionExecutedContext context) { }

        /// <summary>
        /// Get a value from the http header by its key: the header's first value, where it is a guid
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        internal static Guid? GetFromHeader(HttpContext httpContext, string key)
        {
            if (httpContext.Request.Headers.TryGetValue(key, out StringValues correlationIds))
            {
                var correlationId = correlationIds.FirstOrDefault();

                if (Guid.TryParse(correlationId, out Guid id))
                    return id;
            }

            return null;
        }
    }
}

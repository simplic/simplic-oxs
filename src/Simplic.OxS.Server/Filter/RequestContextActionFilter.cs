using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using Simplic.OxS.Server.Services;

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
        /// Create request context
        /// </summary>
        /// <param name="executionContext"></param>
        public void OnActionExecuting(ActionExecutingContext executionContext)
        {
            requestContext.CorrelationId = GetFromHeader(executionContext.HttpContext, Constants.HttpHeaderCorrelationIdKey);

            var authorization = executionContext.HttpContext.Request.Headers.Authorization.ToString()?.Split(" ").ToList();

            if (authorization != null && authorization[0].ToLower() == "bearer")
            {
                requestContext.UserId = GetUserId(executionContext.HttpContext);
                requestContext.TenantId = GetTenantId(executionContext.HttpContext);
            }

            if (authorization != null && authorization[0].ToLower() == Constants.HttpAuthorizationSchemeInternalKey)
            {
                requestContext.UserId = GetFromHeader(executionContext.HttpContext, Constants.HttpHeaderUserIdKey);
                requestContext.TenantId = GetFromHeader(executionContext.HttpContext, Constants.HttpHeaderTenantIdKey);
            }
        }

        /// <summary>
        /// Request ended
        /// </summary>
        /// <param name="context"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void OnActionExecuted(ActionExecutedContext context) { }

        private Guid? GetFromHeader(HttpContext httpContext, string key)
        {
            if (httpContext.Request.Headers.TryGetValue(key, out StringValues correlationIds))
            {
                var correlationId = correlationIds.FirstOrDefault();

                if (Guid.TryParse(correlationId, out Guid id))
                    return id;
            }

            return null;
        }

        /// <summary>
        /// Gets the actual user id from the given jwt token
        /// </summary>
        /// <returns>User id as guid. Null if no user id was found.</returns>
        protected Guid? GetUserId(HttpContext httpContext)
        {
            var claim = httpContext.User.Claims.FirstOrDefault(x => x.Type == "Id");

            if (claim == null)
                return null;

            return Guid.Parse(claim.Value);
        }

        /// <summary>
        /// Gets the actual tenant id from the given jwt token
        /// </summary>
        /// <returns>Tenant id as guid. Null if no tenant id was found.</returns>
        protected Guid? GetTenantId(HttpContext httpContext)
        {
            var claim = httpContext.User.Claims.FirstOrDefault(x => x.Type == "TId");

            if (claim == null)
                return null;

            return Guid.Parse(claim.Value);
        }
    }
}
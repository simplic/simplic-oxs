using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Simplic.OxS.Server.Services;

namespace Simplic.OxS.Server.Middleware
{
    /// <summary>
    /// Middleware for retrieving request context data
    /// </summary>
    internal class RequestContextMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initialize middleware for retreiving context data
        /// </summary>
        /// <param name="next"></param>
        /// <exception cref="ArgumentNullException">Throws an arugment null exception if no "next" delegate exists.</exception>
        public RequestContextMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>
        /// Invoke actual middleware and get request context data
        /// </summary>
        /// <param name="httpContext">Http context instance</param>
        public async Task Invoke(HttpContext httpContext, IRequestContext requestContext)
        {
            if (requestContext is RequestContext context)
            {
                context.CorrelationId = GetFromHeader(httpContext, Constants.HttpHeaderCorrelationIdKey);

                var authorization = httpContext.Request.Headers.Authorization.ToString()?.Split(" ").ToList();

                if (authorization != null && authorization[0].ToLower() == "bearer")
                {
                    context.UserId = GetUserId(httpContext);
                    context.TenantId = GetTenantId(httpContext);
                }

                if (authorization != null && authorization[0].ToLower() == Constants.InternalApiKeyAuth)
                {
                    // TODO: Use consts here
                    context.UserId = GetFromHeader(httpContext, Constants.HttpHeaderUserIdKey);
                    context.TenantId = GetFromHeader(httpContext, Constants.HttpHeaderTenantIdKey);
                }
            }

            await _next(httpContext);
        }

        private Guid? GetFromHeader(HttpContext httpContext, string key)
        {
            if (httpContext.Request.Headers.TryGetValue(Constants.HttpHeaderCorrelationIdKey, out StringValues correlationIds))
            {
                var correlationId = correlationIds.FirstOrDefault(k => k == key);

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
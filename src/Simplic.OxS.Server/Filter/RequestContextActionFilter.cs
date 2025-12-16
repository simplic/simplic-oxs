using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using System;

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

            if (authorization != null && (authorization[0].ToLower() == Constants.HttpAuthorizationSchemeBearerKey
                                            || authorization[0].ToLower() == "v-bearer"))
            {
                requestContext.UserId = GetUserId(executionContext.HttpContext);
                requestContext.OrganizationId = GetOrganizationId(executionContext.HttpContext);
            }

            if (authorization != null && authorization[0].ToLower() == Constants.HttpAuthorizationSchemeInternalKey)
            {
                requestContext.UserId = GetFromHeader(executionContext.HttpContext, Constants.HttpHeaderUserIdKey);
                requestContext.OrganizationId = GetFromHeader(executionContext.HttpContext, Constants.HttpHeaderOrganizationIdKey);
            }

            var apiKey = executionContext.HttpContext.Request.Headers.FirstOrDefault(x => x.Key.Equals(Constants.HttpHeaderApiKey));
            if(!apiKey.Equals(default(KeyValuePair<string, StringValues>)) && !string.IsNullOrEmpty(apiKey.Value))
            {
                requestContext.UserId = GetUserId(executionContext.HttpContext);
                requestContext.OrganizationId = GetOrganizationId(executionContext.HttpContext);
            }

            // Set request header if none are set yet
            if (requestContext.OxSHeaders == null || requestContext.OxSHeaders.Any() == false)
            {
                requestContext.OxSHeaders = new Dictionary<string, string>();
                foreach (var header in requestContext.OxSHeaders)
                {
                    if (header.Key.StartsWith(Simplic.OxS.Constants.OxSHeaderPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        requestContext.OxSHeaders[header.Key] = header.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Request ended
        /// </summary>
        /// <param name="context"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void OnActionExecuted(ActionExecutedContext context) { }

        /// <summary>
        /// Get a value fro the http header by its key
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="key"></param>
        /// <returns></returns>
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

            if (string.IsNullOrWhiteSpace(claim?.Value))
                return null;

            if (Guid.TryParse(claim.Value, out Guid result))
                return result;

            return null;
        }

        /// <summary>
        /// Gets the actual organization id from the given jwt token
        /// </summary>
        /// <returns>Organization id as guid. Null if no organization id was found.</returns>
        protected Guid? GetOrganizationId(HttpContext httpContext)
        {
            var claim = httpContext.User.Claims.FirstOrDefault(x => x.Type == "OId");

            if (string.IsNullOrWhiteSpace(claim?.Value))
                return null;

            if (Guid.TryParse(claim.Value, out Guid result))
                return result;

            return null;
        }
    }
}

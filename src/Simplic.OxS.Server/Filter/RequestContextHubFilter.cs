using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Diagnostics.CodeAnalysis;

namespace Simplic.OxS.Server.Filter
{
    /// <summary>
    /// Middleware for retrieving request context data in signalr hubs
    /// </summary>
    internal class RequestContextHubFilter : IHubFilter
    {
        /// <summary>
        /// Create request context
        /// </summary>
        /// <param name="invocationContext"></param>
        public async ValueTask<object> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object>> next)
        {
            var requestContext = (IRequestContext)invocationContext.ServiceProvider.GetService(typeof(IRequestContext));

            // Get HTTP headers from the SignalR connection context
            var httpContext = invocationContext.Context.GetHttpContext();
            if (httpContext != null)
            {
                // Get correlation ID from header, or generate new one if not present
                requestContext.CorrelationId = GetFromHeader(httpContext, Constants.HttpHeaderCorrelationIdKey) ?? Guid.NewGuid();
                
                // Get other headers if needed
                var userIdFromHeader = GetFromHeader(httpContext, Constants.HttpHeaderUserIdKey);
                var organizationIdFromHeader = GetFromHeader(httpContext, Constants.HttpHeaderOrganizationIdKey);
                
                // Set OxS headers
                SetOxSHeaders(requestContext, httpContext);
            }
            else
            {
                requestContext.CorrelationId = Guid.NewGuid();
            }

            if (invocationContext.Context.User != null)
            {
                requestContext.UserId = GetUserId(invocationContext.Context);
                requestContext.OrganizationId = GetOrganizationId(invocationContext.Context);
            }

            return await next(invocationContext);
        }

        /// <summary>
        /// Get a value from the http header by its key
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private Guid? GetFromHeader(HttpContext httpContext, string key)
        {
            if (httpContext.Request.Headers.TryGetValue(key, out StringValues headerValues))
            {
                var headerValue = headerValues.FirstOrDefault();

                if (Guid.TryParse(headerValue, out Guid id))
                    return id;
            }

            return null;
        }

        /// <summary>
        /// Get a string value from the http header by its key
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private string? GetStringFromHeader(HttpContext httpContext, string key)
        {
            if (httpContext.Request.Headers.TryGetValue(key, out StringValues headerValues))
            {
                return headerValues.FirstOrDefault();
            }

            return null;
        }

        /// <summary>
        /// Set OxS headers in request context
        /// </summary>
        /// <param name="requestContext"></param>
        /// <param name="httpContext"></param>
        private void SetOxSHeaders(IRequestContext requestContext, HttpContext httpContext)
        {
            requestContext.OxSHeaders = new Dictionary<string, string>();
            
            foreach (var header in httpContext.Request.Headers)
            {
                if (header.Key.StartsWith(Simplic.OxS.Constants.OxSHeaderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    requestContext.OxSHeaders[header.Key] = header.Value.FirstOrDefault() ?? "";
                }
            }
        }

        /// <summary>
        /// Gets the actual user id from the given jwt token
        /// </summary>
        /// <returns>User id as guid. Null if no user id was found.</returns>
        protected Guid? GetUserId([NotNull] HubCallerContext context)
        {
            if (context.User == null)
                return null;

            var claim = context.User.Claims.FirstOrDefault(x => x.Type == "Id");

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
        protected Guid? GetOrganizationId([NotNull] HubCallerContext context)
        {
            if (context.User == null)
                return null;

            var claim = context.User.Claims.FirstOrDefault(x => x.Type == "OId");

            if (string.IsNullOrWhiteSpace(claim?.Value))
                return null;

            if (Guid.TryParse(claim.Value, out Guid result))
                return result;

            return null;
        }
    }
}
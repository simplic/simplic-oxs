using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Simplic.OxS.Server.Middleware
{
    /// <summary>
    /// Middleware for managing correlation ids
    /// </summary>
    internal class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initialize middleware for managing correlation ids
        /// </summary>
        /// <param name="next"></param>
        /// <exception cref="ArgumentNullException">Throws an arugment null exception if no "next" delegate exists.</exception>
        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>
        /// Invoke actual middle ware and ensure a correlation id for request and response
        /// </summary>
        /// <param name="httpContext">Http context instance</param>
        public async Task Invoke(HttpContext httpContext)
        {
            string correlationId;
            
            if (httpContext.Request.Headers.TryGetValue(Constants.HttpHeaderCorrelationIdKey, out StringValues correlationIds))
            {
                correlationId = correlationIds.FirstOrDefault(k => k == Constants.HttpHeaderCorrelationIdKey) ??
                                                Guid.NewGuid().ToString();
            }
            else
            {
                // Generate new correlation Id
                correlationId = Guid.NewGuid().ToString();

                // Add correlation id to the actual header - use indexer instead of Add
                httpContext.Request.Headers[Constants.HttpHeaderCorrelationIdKey] = correlationId;
            }

            httpContext.Response.OnStarting(() =>
            {
                // Set actual correlation id
                httpContext.Response.Headers[Constants.HttpHeaderCorrelationIdKey] = correlationId;

                return Task.CompletedTask;
            });

            await _next(httpContext);
        }
    }
}
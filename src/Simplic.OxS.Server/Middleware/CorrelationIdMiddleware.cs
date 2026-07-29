using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.Middleware
{
    /// <summary>
    /// Ensures every request has a correlation id, that the same id is returned to the caller,
    /// and that it is attached to every log entry written while handling the request.
    /// <para>
    /// The id is a <see cref="Guid"/> because <see cref="IRequestContext.CorrelationId"/> is a
    /// <see cref="Guid"/> and <c>Simplic.OxS.InternalClient</c> forwards it to downstream
    /// services. A caller-supplied value that is not a valid <see cref="Guid"/> is therefore
    /// replaced rather than propagated, otherwise the id would silently vanish one hop later.
    /// </para>
    /// </summary>
    internal class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        /// <summary>
        /// Initialize middleware for managing correlation ids
        /// </summary>
        /// <param name="next">Next delegate in the pipeline.</param>
        /// <param name="logger">Logger used to open the correlation scope.</param>
        /// <exception cref="ArgumentNullException">Throws if no "next" delegate exists.</exception>
        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Resolve the correlation id, write it back onto the request and the response, and open
        /// a logging scope so it is present on every log entry for this request.
        /// </summary>
        /// <param name="httpContext">Http context instance</param>
        public async Task Invoke(HttpContext httpContext)
        {
            var correlationId = ResolveCorrelationId(httpContext);

            // Write back onto the *request* so everything reading the header downstream agrees
            // with what the caller is told. RequestContextActionFilter populates
            // IRequestContext.CorrelationId from this header, and OxSExceptionHandler reports it
            // — previously a generated id was only ever put on the response, so the id returned
            // to the caller appeared in no log entry.
            httpContext.Request.Headers[Constants.HttpHeaderCorrelationIdKey] = correlationId.ToString();

            httpContext.Response.OnStarting(() =>
            {
                httpContext.Response.Headers[Constants.HttpHeaderCorrelationIdKey] = correlationId.ToString();

                return Task.CompletedTask;
            });

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
            }))
            {
                await _next(httpContext);
            }
        }

        /// <summary>
        /// Use the caller's correlation id when it is a usable <see cref="Guid"/>, otherwise mint
        /// a new one.
        /// </summary>
        private static Guid ResolveCorrelationId(HttpContext httpContext)
        {
            var incoming = httpContext.Request.Headers[Constants.HttpHeaderCorrelationIdKey].FirstOrDefault();

            return Guid.TryParse(incoming, out var parsed) && parsed != Guid.Empty
                ? parsed
                : Guid.NewGuid();
        }
    }
}

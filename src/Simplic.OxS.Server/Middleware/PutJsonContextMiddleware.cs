using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Simplic.OxS.Server.Middleware
{
    /// <summary>
    /// Middleware for managing correlation ids
    /// </summary>
    internal class PutJsonContextMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initialize middleware for setting json content as raw-item content
        /// </summary>
        /// <param name="next"></param>
        /// <exception cref="ArgumentNullException">Throws an arugment null exception if no "next" delegate exists.</exception>
        public PutJsonContextMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>
        /// Invoke actual middle and add raw json to http context
        /// </summary>
        /// <param name="httpContext">Http context instance</param>
        public async Task Invoke(HttpContext httpContext)
        {
            // if its an update?
            if (httpContext.Request.Method?.ToUpper() == "PATCH")
            {
                // enable buffering in order to see raw object
                httpContext.Request.EnableBuffering();

                using MemoryStream memoryStream = new();

                // save body to memory
                await httpContext.Request.Body.CopyToAsync(memoryStream);

                // convert it to json
                var rawJson = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());

                // save raw json on current context. 
                httpContext.Items.Add("rawJson", rawJson);

                // reset the position
                httpContext.Request.Body.Position = 0;
            }

            await _next(httpContext);
        }
    }
}
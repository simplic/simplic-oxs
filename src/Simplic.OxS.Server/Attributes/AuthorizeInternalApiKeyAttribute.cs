using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Simplic.OxS.Server
{
    /// <summary>
    /// Attribute for securing an internal api controller.
    /// </summary>
    [AttributeUsage(validOn: AttributeTargets.Class)]
    public class AuthorizeInternalApiKeyAttribute : Attribute, IAsyncActionFilter
    {
        /// <summary>
        /// Will be executed by the asp.net core runtime for filtering user access.
        /// </summary>
        /// <param name="context">Current request context</param>
        /// <param name="next">Next request step</param>
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();

            if (string.IsNullOrWhiteSpace(authHeader))
            {
                context.Result = GetUnauthorized();
                return;
            }

            var authParts = authHeader.Split(" ").ToList();

            if (authParts.Count != 2)
            {
                context.Result = GetUnauthorized();
                return;
            }

            if (authParts[0] != "i-api-key")
            {
                context.Result = GetUnauthorized();
                return;
            }

            var appSettings = context.HttpContext.RequestServices.GetRequiredService<IOptions<Settings.AuthSettings>>();

            // Check whether the actual api key is correct.
            if (appSettings.Value.InternalApiKey != authParts[1])
            {
                context.Result = GetUnauthorized();
                return;
            }

            if (!(context.Controller is Controller.OxSInternalController))
            {
                context.Result = new ContentResult()
                {
                    StatusCode = 400,
                    Content = "Internal calls are only allowed for OxSInternalController. Inherit from `OxSInternalController` for internal controller usage."
                };
            }

            await next();
        }

        /// <summary>
        /// Gets a result that determines the unauthorized state
        /// </summary>
        /// <returns>Content result (StatusCode = 401)</returns>
        private ContentResult GetUnauthorized()
        {
            return new ContentResult()
            {
                StatusCode = 401,
                Content = "Api Key is not valid or not provided"
            };
        }
    }
}

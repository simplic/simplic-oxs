using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

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

            if (authParts[0] != Constants.HttpAuthorizationSchemeInternalKey)
            {
                context.Result = GetUnauthorized();
                return;
            }

            var appSettings = context.HttpContext.RequestServices.GetRequiredService<IOptions<OxS.Settings.AuthSettings>>();

            // Check whether the actual api key is correct.
            if (!IsConfiguredKey(appSettings.Value.InternalApiKey, authParts[1]))
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

                // A result set here only reaches the caller when the action is not executed.
                return;
            }

            await next();
        }

        /// <summary>
        /// Whether the presented key is the configured one. A configured key that is null, empty
        /// or whitespace admits nobody: a host without a key is closed, never open. The
        /// comparison takes the same time wherever the first difference is, so the key cannot be
        /// recovered from response times.
        /// </summary>
        /// <param name="configuredKey">The key from the host's settings</param>
        /// <param name="presentedKey">The key from the request</param>
        /// <returns>True when the request may pass</returns>
        private static bool IsConfiguredKey(string? configuredKey, string presentedKey)
        {
            if (string.IsNullOrWhiteSpace(configuredKey))
                return false;

            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configuredKey), Encoding.UTF8.GetBytes(presentedKey));
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
                Content = $"Internal-api-key ({Constants.HttpAuthorizationSchemeInternalKey}) is not valid or not provided"
            };
        }
    }
}

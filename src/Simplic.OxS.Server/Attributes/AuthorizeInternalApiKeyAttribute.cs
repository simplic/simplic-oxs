using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Simplic.OxS.Server
{
    [AttributeUsage(validOn: AttributeTargets.Class)]
    public class AuthorizeInternalApiKeyAttribute : Attribute, IAsyncActionFilter
    {
        private ContentResult GetUnauthorized()
        {
            return new ContentResult()
            {
                StatusCode = 401,
                Content = "Api Key is not valid or not provided"
            };
        }

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

            // Check whether the request is of type http. Internally only http requests are allowed
            // Maybe we should only allow do this in prod?
            //if (context.HttpContext.Request.Scheme.ToLower() != "http")
            //{
            //    context.Result = new ContentResult()
            //    {
            //        StatusCode = 400,
            //        Content = "Only http is allowed for internal requests."
            //    };
            //}

            if (context.Controller is Controller.OxSInternalController controller)
            {
                if (context.HttpContext.Request.Headers.TryGetValue("TenantId", out StringValues tenantValues))
                {
                    var tenantValue = tenantValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(tenantValue))
                        controller.TenantId = Guid.Parse(tenantValue);
                }

                if (context.HttpContext.Request.Headers.TryGetValue("UserId", out StringValues userValues))
                {
                    var userValue = userValues.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(userValue))
                        controller.UserId = Guid.Parse(userValue);
                }
            }
            else
            {
                context.Result = new ContentResult()
                {
                    StatusCode = 400,
                    Content = "Internal calls are only allowed for OxSInternalController. Inherit from `OxSInternalController` for internal controller usage."
                };
            }

            await next();
        }
    }
}

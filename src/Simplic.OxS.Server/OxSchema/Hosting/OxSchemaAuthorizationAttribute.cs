using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Simplic.OxS.Server.OxSchema
{
    /// <summary>
    /// Applies the host's default authorization policy to an anonymous action when the host
    /// turned <see cref="OxSchemaBuildOptions.RequireAuthorization"/> on, and does nothing at all
    /// when it did not.
    /// </summary>
    /// <remarks>
    /// The action stays <c>[AllowAnonymous]</c>, so with the option off neither the default
    /// policy nor a fallback policy of the host applies to it. With the option on, the caller is
    /// authenticated and authorized against the default policy - the one <c>[Authorize]</c>
    /// names - and refused with that policy's own challenge or forbid, so the answer is the one
    /// every authorized route of the host gives.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class OxSchemaAuthorizationAttribute : Attribute, IAsyncAuthorizationFilter
    {
        /// <inheritdoc/>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var services = context.HttpContext.RequestServices;

            if (!services.GetRequiredService<OxSchemaRegistry>().RequireAuthorization)
                return;

            var policy = await services.GetRequiredService<IAuthorizationPolicyProvider>().GetDefaultPolicyAsync();
            var evaluator = services.GetRequiredService<IPolicyEvaluator>();

            var authentication = await evaluator.AuthenticateAsync(policy, context.HttpContext);
            var authorization = await evaluator.AuthorizeAsync(policy, authentication, context.HttpContext, context);

            if (authorization.Challenged)
                context.Result = new ChallengeResult([.. policy.AuthenticationSchemes]);
            else if (authorization.Forbidden)
                context.Result = new ForbidResult([.. policy.AuthenticationSchemes]);
        }
    }
}

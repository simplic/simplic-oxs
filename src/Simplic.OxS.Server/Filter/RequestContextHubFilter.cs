using Microsoft.AspNetCore.SignalR;
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
        /// <param name="executionContext"></param>
        public async ValueTask<object> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object>> next)
        {
            var requestContext = (IRequestContext)invocationContext.ServiceProvider.GetService(typeof(IRequestContext));

            requestContext.CorrelationId = Guid.NewGuid();

            if (invocationContext.Context.User != null)
            {
                requestContext.UserId = GetUserId(invocationContext.Context);
                requestContext.OrganizationId = GetOrganizationId(invocationContext.Context);
            }

            return await next(invocationContext);
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
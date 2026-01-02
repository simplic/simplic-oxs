using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using HotChocolate.Language;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Server.Middleware
{
	internal class HttpRequestInterceptor : DefaultHttpRequestInterceptor
	{
		public override ValueTask OnCreateAsync(HttpContext context,
			IRequestExecutor requestExecutor, OperationRequestBuilder requestBuilder,
			CancellationToken cancellationToken)
		{
			if (context.RequestServices.GetService(typeof(IRequestContext)) is IRequestContext requestContext)
			{ 
				requestContext.UserId = GetUserId(context);
				requestContext.OrganizationId = GetOrganizationId(context);

				// Set correlation id (http defaults)
				string correlationId;

				if (context.Request.Headers.TryGetValue(Constants.HttpHeaderCorrelationIdKey, out StringValues correlationIds))
				{
					correlationId = correlationIds.FirstOrDefault(k => k == Constants.HttpHeaderCorrelationIdKey) ??
													Guid.NewGuid().ToString();
				}
				else
				{
					// Generate new correlation Id
					correlationId = Guid.NewGuid().ToString();

					// Add correlation id to the actual header - use indexer instead of Add
					context.Request.Headers[Constants.HttpHeaderCorrelationIdKey] = correlationId;
				}

				// TODO: Optimize that one / use guid from above
				requestContext.CorrelationId = Guid.Parse(correlationId);
			}

			return base.OnCreateAsync(context, requestExecutor, requestBuilder, cancellationToken);
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

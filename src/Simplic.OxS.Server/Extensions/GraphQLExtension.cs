using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HotChocolate.Execution.Configuration;
using Simplic.OxS.Server.Middleware;
using Simplic.OxS.Server.Abstract;
using Microsoft.AspNetCore.Builder;

namespace Simplic.OxS.Server.Extensions
{
	public static class GraphQLExtension
	{
		/// <summary>
		/// Enable the use of GraphQL within the simplic eco system 
		/// In order to work you will need to implement the <see cref="IQueryBase"/>
		/// </summary>
		/// <param name="services"></param>
		/// <returns></returns>
		public static IServiceCollection UseGraphQL<TQuery>(this IServiceCollection services) where TQuery : class
		{
			services.AddGraphQLServer()
						.AddHttpRequestInterceptor<HttpRequestInterceptor>()
						.AddAuthorization()
						.AddQueryType<TQuery>()
						.AddMongoDbFiltering()
						.AddMongoDbProjections()
						.AddMongoDbSorting();


			return services;
		}
	}
}

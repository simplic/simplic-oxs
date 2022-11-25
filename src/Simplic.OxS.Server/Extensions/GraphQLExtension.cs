﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HotChocolate.Execution.Configuration;
using Simplic.OxS.Server.Middleware;
using Simplic.OxS.Server.Abstract;

namespace Simplic.OxS.Server.Extensions
{
	internal static class GraphQLExtension
	{
		/// <summary>
		/// Enable the use of GraphQL within the simplic eco system 
		/// In order to work you will need to implement the <see cref="QueryBase"/>
		/// </summary>
		/// <param name="services"></param>
		/// <returns></returns>
		internal static IServiceCollection UseSimplicGraphQL(this IServiceCollection services)
		{
			services.AddGraphQLServer()
						.AddHttpRequestInterceptor<HttpRequestInterceptor>()
						.AddAuthorization()
						.AddQueryType<QueryBase>()
						.AddMongoDbFiltering()
						.AddMongoDbProjections()
						.AddMongoDbSorting();
			return services;
		}
	}
}

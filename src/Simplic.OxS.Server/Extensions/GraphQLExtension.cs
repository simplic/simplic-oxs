using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Server.Abstract;
using Simplic.OxS.Server.Middleware;

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
        public static IServiceCollection UseGraphQL<TQuery>(this IServiceCollection services, Action<IRequestExecutorBuilder> builder = null) where TQuery : class
        {
            var req = services.AddGraphQLServer()
                        .AddHttpRequestInterceptor<HttpRequestInterceptor>()
                        .AddAuthorization()
                        .AddQueryType<TQuery>();

            builder?.Invoke(req);

            req.AddMongoDbFiltering()
               .AddMongoDbProjections()
               .AddMongoDbSorting();

            return services;
        }
    }
}

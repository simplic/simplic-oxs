using HotChocolate.Execution.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Server.GraphQL;
using Simplic.OxS.Server.Middleware;

namespace Simplic.OxS.Server.Extensions
{
    public static class GraphQLExtension
    {
        /// <summary>
        /// Enable the use of GraphQL within the simplic eco system.
        /// </summary>
        /// <param name="services">DI service collection.</param>
        /// <param name="builder">Optional builder hook for service-specific extensions.</param>
        /// <param name="tolerateMissingFieldValues">
        /// When <c>true</c> (default), every output field on user-defined object types is
        /// made nullable in the generated schema. This prevents <c>NonNull</c> spec
        /// violations when a resolver returns <c>null</c> for a property that wasn't
        /// stored on legacy documents. Set to <c>false</c> for strict, spec-conformant
        /// non-null behavior (clients then must handle the propagated <c>null</c>).
        /// </param>
        public static IServiceCollection UseGraphQL<TQuery>(
            this IServiceCollection services,
            Action<IRequestExecutorBuilder> builder = null,
            bool tolerateMissingFieldValues = true) where TQuery : class
        {
            var req = services.AddGraphQLServer().ModifyOptions(o =>
            {
                o.DefaultQueryDependencyInjectionScope = DependencyInjectionScope.Request;
                o.DefaultMutationDependencyInjectionScope = DependencyInjectionScope.Request;
            })
            .AddHttpRequestInterceptor<HttpRequestInterceptor>()
            .AddAuthorization()
            .AddQueryType<TQuery>();

            if (tolerateMissingFieldValues)
                req.TryAddTypeInterceptor<MakeFieldsNullableTypeInterceptor>();

            // Set TimeSpan representation to d.hh:mm:ss
            req.AddType(new TimeSpanType(TimeSpanFormat.DotNet));

            builder?.Invoke(req);

            req.AddMongoDbPagingProviders()
                .AddMongoDbProjections()
                .AddMongoDbFiltering()
                .AddMongoDbSorting();

            return services;
        }
    }
}

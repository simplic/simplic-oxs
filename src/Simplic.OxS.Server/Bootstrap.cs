using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Simplic.OxS.Data;
using Simplic.OxS.InternalClient;
using Simplic.OxS.MessageBroker;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.Server.Filter;
using Simplic.OxS.Server.Middleware;
using Simplic.OxS.Server.Services;
using StackExchange.Redis;

namespace Simplic.OxS.Server
{
    /// <summary>
    /// Base class for implementing a Simplic.OxS microservice
    /// </summary>
    public abstract class Bootstrap
    {
        /// <summary>
        /// Initialize web api and configure services. Will be called from the host-builder.
        /// </summary>
        /// <param name="configuration">Configuration service</param>
        /// <param name="currentEnvironment">Environment instance</param>
        public Bootstrap(IConfiguration configuration, IWebHostEnvironment currentEnvironment)
        {
            Configuration = configuration;
            CurrentEnvironment = currentEnvironment;
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">Service collection</param>
        public virtual void ConfigureServices(IServiceCollection services)
        {
            Console.WriteLine($"Configure for env: {CurrentEnvironment.EnvironmentName}");

            // Add logging and tracing systems
            services.AddLoggingAndMetricTracing(Configuration, ServiceName);

            // Add Redis caching
            services.AddRedisCaching(Configuration, out string connection);

            // Add MongoDb context and bind configuration
            services.AddMongoDb(Configuration);

            // Add RabbitMq context and bind configuration
            services.AddRabbitMQ(Configuration, ConfigureEndpointConventions);

            // Add Jwt authentication and bind configuration
            services.AddJwtAuthentication(Configuration);

            // Register custom services
            RegisterServices(services);

            // Create mapper profiles and register mapper
            var mapperConfig = new MapperConfiguration(RegisterMapperProfiles);

            IMapper mapper = mapperConfig.CreateMapper();
            services.AddSingleton(mapper);

            services.AddTransient<IMapService, MapService>();

            var redisUrl = Configuration.GetValue<string>("Redis:RedisCacheUrl");
            var existingConnection = ConnectionMultiplexer.Connect(redisUrl);
            services.AddTransient<IResourceLockingService, ResourceLockingService>(x => new ResourceLockingService(existingConnection));

            // Add internal services
            services.AddScoped<IRequestContext, RequestContext>();
            services.AddScoped<RequestContextActionFilter>();
            services.AddScoped<IInternalClient, InternalClientBase>();

            // Register web-api controller. Must be executed before creating swagger configuration
            services.AddControllers(o =>
            {
                o.Filters.Add(typeof(RequestContextActionFilter));
            });

            services.AddSwagger(CurrentEnvironment, ApiVersion, ServiceName, GetApiInformation());

            // Add signalr
            if (string.IsNullOrWhiteSpace(connection))
                services.AddSignalR(hubOptions =>
                {
                    hubOptions.AddFilter<RequestContextHubFilter>();
                });
            else
                services.AddSignalR(hubOptions =>
                {
                    hubOptions.AddFilter<RequestContextHubFilter>();
                }).AddStackExchangeRedis(connection);
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app">Application context</param>
        /// <param name="env">Env context</param>
        public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Allow paths like /<service-name>-api/v1
            var basePath = $"/{ServiceName.ToLower()}-api/{ApiVersion}";
            app.UsePathBase(basePath);

            if (env.IsDevelopment() || env.EnvironmentName.ToLower() == "local")
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger(c =>
            {
                c.PreSerializeFilters.Add((swagger, httpReq) =>
                {
                    if (env.IsDevelopment())
                    {
                        swagger.Servers = new List<OpenApiServer> { new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}{basePath}" } };
                    }
                    else
                    {
                        swagger.Servers = new List<OpenApiServer> { new OpenApiServer { Url = $"https://{httpReq.Host.Value}{basePath}" } };
                    }
                });
            });

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint($"{basePath}/swagger/{ApiVersion}/swagger.json", $"Simplic.OxS.{ServiceName} {ApiVersion}");
                c.SwaggerEndpoint($"{basePath}/swagger/{ApiVersion}-SignalR/swagger.json", $"Simplic.OxS.{ServiceName} {ApiVersion}-SignalR");
            });

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<PutJsonContextMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                MapHubs(endpoints);

                MapEndpoints(endpoints);
            });
        }

        /// <summary>
        /// Method for SignalR hub registration
        /// </summary>
        /// <param name="builder">Builder instance</param>
        protected virtual void MapHubs(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder builder) { }

        /// <summary>
        /// Method for mapping endpoint routings
        /// </summary>
        /// <param name="builder">Builder instance</param>
        protected virtual void MapEndpoints(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder builder) { }

        /// <summary>
        /// Get api information for the current service
        /// </summary>
        /// <returns>Api info instance</returns>
        protected virtual OpenApiInfo GetApiInformation()
        {
            return new OpenApiInfo
            {
                Version = ApiVersion,
                Title = $"`{ServiceName}` service api",
                Description = "Contains http/https endpoints for working with the Simplic.OxS/Ox apis.",
                TermsOfService = new Uri("https://simplic.biz/datenschutzerklaerung/"),
                Contact = new OpenApiContact
                {
                    Name = "SIMPLIC GmbH",
                    Email = "post@simplic.biz",
                    Url = new Uri("https://simplic.biz/kontakt/")
                },
                License = new OpenApiLicense
                {
                    Name = "Simplic.OxS OpenAPI-License",
                    Url = new Uri("https://simplic.biz/ox-api-license")
                }
            };
        }

        /// <summary>
        /// Method for custom profile registrations. (AutoMapper)
        /// </summary>
        /// <param name="mapperConfiguration">Mapper configuration instance. Add profiles to this mapping.</param>
        protected virtual void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration) { }

        /// <summary>
        /// Method for custom endpoint registration (MassTransit/RabbitMq). Register commands in this method.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="settings">MessageBroker settings</param>
        protected virtual void ConfigureEndpointConventions(IServiceCollection services, MessageBrokerSettings settings) { }

        /// <summary>
        /// Will be called for registering custom services.
        /// </summary>
        /// <param name="services">Service collection for adding additional services</param>
        protected abstract void RegisterServices(IServiceCollection services);

        /// <summary>
        /// Gets the current service name. This will not contains Simplic.OxS.
        /// </summary>
        protected abstract string ServiceName { get; }

        /// <summary>
        /// Gets the actual service version. Default is v1.
        /// </summary>
        protected virtual string ApiVersion { get; } = "v1";

        /// <summary>
        /// Gets the current configuration service.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Gets or sets the current environment.
        /// </summary>
        private IWebHostEnvironment CurrentEnvironment { get; set; }
    }
}

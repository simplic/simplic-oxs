using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using MongoDB.Bson;
using Simplic.OxS.Data;
using Simplic.OxS.InternalClient;
using Simplic.OxS.MessageBroker;
using Simplic.OxS.ModelDefinition.Extension;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.Server.Filter;
using Simplic.OxS.Server.Middleware;
using Simplic.OxS.Server.Service;
using Simplic.OxS.Server.Services;
using Simplic.OxS.ServiceDefinition;
using Simplic.OxS.ServiceDefinition.Repository;
using Simplic.OxS.Settings.Abstractions;

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
            MongoDB.Bson.Serialization.BsonSerializer.RegisterSerializer(new MongoDB.Bson.Serialization.Serializers.GuidSerializer(GuidRepresentation.Standard));

            Console.WriteLine($"Configure for env: {CurrentEnvironment.EnvironmentName}");

            // Add logging and tracing systems
            services.AddLoggingAndMetricTracing(Configuration, ServiceName);

            // Add Redis caching
            services.AddRedisCaching(Configuration, out string connection);

            // Add MongoDb context and bind configuration
            services.AddMongoDb(Configuration);

            // Add RabbitMq context and bind configuration
            services.AddRabbitMQ(Configuration, ConfigureEndpointConventions);

            // Add Grpc Server
            services.AddGrpcServer();

            // Add Jwt authentication and bind configuration
            var authBuilder = services.AddAuthentication(Configuration);
            if (authBuilder != null)
                ConfigureAuthentication(authBuilder);


            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "ApiKey")
                    .RequireAuthenticatedUser()
                    .Build();

                // New policy explicitly forbidding API Key authentication
                options.AddPolicy("JwtOnly", policy =>
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme) // Only allow JWT
                          .RequireAuthenticatedUser());
            });

            // Add MCP server if enabled and available
            if (IsMcpEnabled())
            {
                ConfigureMcpServicesIfAvailable(services);
            }

            // Add organization settings if configured
            var settingsConfig = ConfigureOrganizationSettings();
            if (settingsConfig != null)
            {
                services.AddOrganizationSettingsWithMongo(settingsConfig, ServiceName);
            }

            // Register custom services
            RegisterServices(services);

            // Create mapper profiles and register mapper
            services.AddSingleton(provider =>
            {
                var loggerFactory = provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
                var mapperConfig = new MapperConfiguration(RegisterMapperProfiles, loggerFactory);

                return mapperConfig.CreateMapper();
            });

            services.AddTransient<IMapService, MapService>();

            // Add internal services
            services.AddScoped<IRequestContext, RequestContext>();
            services.AddScoped<RequestContextActionFilter>();
            services.AddScoped<ValidationActionFilter>();
            services.AddScoped<IInternalClient, InternalClientBase>();
            services.AddScoped<IEndpointContractRepository, EndpointContractRepository>();
            services.AddSingleton<ServiceDefinitionService>((x) =>
            {
                var f = new ServiceDefinitionService
                {
                    ServiceName = ServiceName,
                    Version = ApiVersion
                };

                f.Fill();

                return f;
            });

            // Register web-api controller. Must be executed before creating swagger configuration
            MvcBuilder(services.AddControllers(o =>
            {
                o.Filters.Add<RequestContextActionFilter>();
                o.Filters.Add<ValidationActionFilter>();
            }));

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

            var modelDefinitionBuilderConfig = ConfigureModelDefinitions();
            if (modelDefinitionBuilderConfig.Count != 0)
                app.AddControllerDefinitions(env, basePath, modelDefinitionBuilderConfig);

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<PutJsonContextMiddleware>();
            app.UseMiddleware<ErrorLoggingMiddleware>();

            // Configure MCP server if enabled and available
            if (IsMcpEnabled())
            {
                ConfigureMcpApplicationIfAvailable(app);
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                MapHubs(endpoints);

                MapEndpoints(endpoints);
            });
        }

        /// <summary>
        /// Will be called after controllers are created
        /// </summary>
        /// <param name="builder"><see cref="IMvcBuilder"/> instance</param>
        protected virtual void MvcBuilder(IMvcBuilder builder) { }

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
        /// Method for mapping grpc services
        /// </summary>
        /// <param name="builder">Builder instance</param>
        protected virtual void MapGrpcServices(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder builder) { }

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
        /// Method that should be used for added additional auth schemes/methods
        /// </summary>
        /// <param name="authBuilder">Authentication builder instance, for adding additional auth scheme</param>
        protected virtual void ConfigureAuthentication(AuthenticationBuilder authBuilder) { }

        /// <summary>
        /// Method that should return all controllers that are used to build model definitions
        /// </summary>
        /// <returns></returns>
        protected virtual IList<Type> ConfigureModelDefinitions() { return new List<Type>(); }

        /// <summary>
        /// Method for configuring organization settings. Return null to disable settings.
        /// </summary>
        /// <returns>Settings configuration action or null</returns>
        protected virtual Action<IOrganizationSettingsBuilder>? ConfigureOrganizationSettings() { return null; }

        /// <summary>
        /// Method for configuring MCP services. Override to register custom MCP tools.
        /// This method is only called if IsMcpEnabled() returns true and MCP dependencies are available.
        /// </summary>
        /// <param name="services">Service collection</param>
        protected virtual void ConfigureMcpServices(IServiceCollection services) { }

        /// <summary>
        /// Determines whether MCP server is enabled for this service. Default is false.
        /// </summary>
        /// <returns>True if MCP should be enabled, false otherwise</returns>
        protected virtual bool IsMcpEnabled() { return false; }

        /// <summary>
        /// Configures MCP services if the MCP packages are available
        /// </summary>
        /// <param name="services">Service collection</param>
        private void ConfigureMcpServicesIfAvailable(IServiceCollection services)
        {
            try
            {
                // Try to load MCP types using reflection to avoid compile-time dependencies
                var mcpServerAssembly = System.Reflection.Assembly.Load("Simplic.OxS.Mcp.Server");
                var extensionsType = mcpServerAssembly.GetType("Simplic.OxS.Mcp.Server.Extensions.ServiceCollectionExtensions");

                if (extensionsType != null)
                {
                    var addMcpServerMethod = extensionsType.GetMethod("AddMcpServer",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                    if (addMcpServerMethod != null)
                    {
                        addMcpServerMethod.Invoke(null, new object[] { services });
                        ConfigureMcpServices(services);
                        Console.WriteLine("MCP Server configured successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MCP Server not available: {ex.Message}");
                Console.WriteLine("To enable MCP functionality, add references to Simplic.OxS.Mcp and Simplic.OxS.Mcp.Server packages");
            }
        }

        /// <summary>
        /// Configures MCP application if the MCP packages are available
        /// </summary>
        /// <param name="app">Application builder</param>
        private void ConfigureMcpApplicationIfAvailable(IApplicationBuilder app)
        {
            try
            {
                var mcpServerAssembly = System.Reflection.Assembly.Load("Simplic.OxS.Mcp.Server");
                var extensionsType = mcpServerAssembly.GetType("Simplic.OxS.Mcp.Server.Extensions.ApplicationBuilderExtensions");

                if (extensionsType != null)
                {
                    var useMcpServerMethod = extensionsType.GetMethod("UseMcpServer",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                    if (useMcpServerMethod != null)
                    {
                        useMcpServerMethod.Invoke(null, new object[] { app });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MCP Server configuration skipped: {ex.Message}");
            }
        }

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

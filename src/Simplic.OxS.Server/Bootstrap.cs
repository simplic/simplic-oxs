using AutoMapper;
using DnsClient.Internal;
using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OxQL.AspNetCore;
using OxQL.Core;
using OxQL.Core.Engine;
using OxQL.Model.Addon;
using OxQL.Mongo;
using OxQL.Studio;
using Simplic.OxS.Data;
using Simplic.OxS.InternalClient;
using Simplic.OxS.MessageBroker;
using Simplic.OxS.Server.Exceptions;
using Simplic.OxS.Server.Exceptions.Handlers;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.Server.Filter;
using Simplic.OxS.Server.Middleware;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Service;
using Simplic.OxS.Server.Services;
using Simplic.OxS.Server.Settings;
using Simplic.OxS.ServiceDefinition;
using Simplic.OxS.ServiceDefinition.Repository;
using Simplic.OxS.Settings.Abstractions;
using System.Reflection;

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

            services.AddSingleton<ICurrentService>(x => new Implementations.CurrentService
            {
                ServiceName = ServiceName,
                ApiVersion = ApiVersion
            });

            // Add logging and tracing systems
            services.AddLoggingAndMetricTracing(Configuration, ServiceName, ApiVersion, CurrentEnvironment.EnvironmentName);

            // Add Redis caching
            services.AddRedisCaching(Configuration, out string connection);

            // Add MongoDb context and bind configuration
            services.AddMongoDb(Configuration);

            // The service's class maps and serializers, before anything looks a serializer up.
            EnsureBsonClassMaps();

            // Add RabbitMq context and bind configuration
            services.AddRabbitMQ(Configuration, ConfigureEndpointConventions);

            // Add Grpc Server
            services.AddGrpcServer(ConfigureGrpc);

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

            // Add organization settings if configured
            var settingsConfig = ConfigureOrganizationSettings();
            if (settingsConfig != null)
            {
                services.AddOrganizationSettingsWithMongo(settingsConfig, ServiceName);
            }

            // Register custom services
            RegisterServicesInto(services);

            // Create mapper profiles and register mapper
            services.AddSingleton(provider => CreateMapper());

            services.AddTransient<IMapService, MapService>();

            // Add internal services
            services.AddScoped<IRequestContext, RequestContext>();
            services.AddScoped<RequestContextActionFilter>();
            services.AddScoped<ValidationActionFilter>();
            services.AddScoped<IInternalClient, InternalClientBase>();
            services.AddScoped<IEndpointContractRepository, EndpointContractRepository>();
            services.AddScoped<IAddonFieldRepository, AddonFieldRepository>();
            services.AddScoped<IAddonDefinitionRepository, AddonDefinitionRepository>();
            services.AddSingleton<ServiceDefinitionService>((x) =>
            {
                var f = new ServiceDefinitionService(x, x.GetRequiredService<ILogger<ServiceDefinitionService>>())
                {
                    ServiceName = ServiceName,
                    Version = ApiVersion
                };

                f.Fill();

                return f;
            });

            // ── OxQL core ───────────────────────────────────────────────────────────
            // Every option of the engine comes from the host's `OxQL` section (compat mode,
            // explain, limits, timeouts, representation tolerance, caches); /schema publishes
            // this very options object, so there is nowhere for the two to drift apart. The
            // cursor signing key is derived from the auth token unless the section names one.
            Console.WriteLine("Add OxQL core");

            // The engine reads an organisation's addon definitions per request through this
            // source; registered before the core so the engine's empty default does not win.
            // A host that is configured with no database gets a source that needs none, so the
            // routes that are handed one without reading it (the schema document, health) answer.
            var mongodb = Configuration.GetSection("MongoDB").Get<Data.MongoDB.ConnectionSettings>();

            services.AddSingleton<AddonDefinitionCache>();

            if (mongodb != null)
                services.AddScoped<IAddonDefinitionSource, AddonDefinitionSource>();
            else
                services.AddScoped<IAddonDefinitionSource, NoDatabaseAddonDefinitionSource>();

            services.AddOxQLCore(Configuration.GetSection("OxQL"), options =>
            {
                options.Cursor.SigningKey ??= Configuration.GetSection("Auth").Get<OxS.Settings.AuthSettings>()?.Token;
            });

            // ── Ox schema ───────────────────────────────────────────────────────────
            // Built while the host starts, held in memory, never written to disk.
            var typeAssemblies = OxQLTypeAssemblies();

            // The mapper is created before the schema is built, whoever asks for the schema
            // first: RegisterMapperProfiles is where services have always registered class maps,
            // and the build freezes the class map of every type it reaches. A fault in the
            // profiles therefore stops the start, in every environment, with the original exception.
            // The host's registration is the one that stands: a schema the service registered
            // itself is merged into it or replaced, and the start says so (ConfigureOxSchema is
            // the place for a service's schema options).
            services.AddHostOxSchema(
                schema => FillOxSchema(schema, typeAssemblies),
                provider => provider.GetRequiredService<IMapper>());

            // ── OxQL MongoDB adapter ────────────────────────────────────────────────
            // Change the connection string in appsettings.json (or via environment variable
            if (mongodb != null)
            {
                Console.WriteLine("Add OxQL MongoDB");

                services.AddOxQLMongo(options =>
                {
                    options.ConnectionString = mongodb.ConnectionString;
                    options.DatabaseName = mongodb.Database;

                    // Engine faults carry their message only where a developer reads it.
                    options.IncludeErrorDetails = CurrentEnvironment.IsDevelopment()
                        || string.Equals(CurrentEnvironment.EnvironmentName, "local", StringComparison.OrdinalIgnoreCase);

                    // The schema's startup filter hands the engine its model; this list is the
                    // engine's own fallback for a host that never builds a schema.
                    options.ScanAssemblies(typeAssemblies);
                });
            }
            else
            {
                // Without a database the query routes are constructed all the same: health
                // answers, and a query fails with a message that names the missing section.
                services.TryAddSingleton<IQueryEngine, NoDatabaseQueryEngine>();
            }

            // ── OxQL ASP.NET Core controller ────────────────────────────────────────
            // Routes: POST /oxql/query, POST /oxql/batch, GET /oxql/health, POST /oxql/explain
            // (on unless OxQL:Explain:Enabled is false, then 404). Authenticated like every other
            // controller. Their internal twins (internal/oxql/batch, internal/oxql/explain) are
            // OxQLInternalController's, admitted by the internal api key.
            // Whether its remote reference check stops the host under continuous integration
            // is the schema's decision (AddOxSchema), so both checks fail fast on one value.
            Console.WriteLine("Add OxQL ASP.NET Core");

            // Ahead of the engine's check of the remote references, which reads the same owners:
            // an InternalHosts value that is no host is named as that, not as a missing entry.
            services.AddTransient<IStartupFilter, RemoteOwnerHostCheck>();

            services.AddOxQLAspNetCore(options =>
            {
                options.RequireAuthorization = true;
            });

            // ── The organisation scope ──────────────────────────────────────────────
            // The engine applies `organizationId eq <request context>` at every entry into an
            // entity and refuses to start without a provider; this one reads the request
            // context, filled from the bearer token or, on an internal call, from the headers.
            services.AddOxQLScope<OxQLScopeProvider>();

            // ── Remote resolve ──────────────────────────────────────────────────────
            // Resolves and semi-joins into an entity another service owns go to that owner's
            // internal batch route (InternalHosts / InternalApiVersions), one message per call,
            // over a client that waits OxQL:Remote:ConnectTimeoutMs for a connection;
            // explain checks the parts continued there at the owner's internal explain route,
            // which counts its places in flight by the service name this client sends.
            RemoteQueryClient.AddHttpClient(services, Configuration);
            services.AddSingleton<IRemoteQueryClient>(provider =>
            {
                var client = ActivatorUtilities.CreateInstance<RemoteQueryClient>(provider);

                client.Caller = ServiceName;

                return client;
            });

            // One line per referenced owner at start: the route its calls take.
            services.AddTransient<IStartupFilter, RemoteOwnerRouteLog>();

            // OxQL Studio: shape from /schema and /schema/addons, execution through /oxql.
            if (OxQLStudioEnabled(Configuration, CurrentEnvironment))
                services.AddOxQLStudio(options => ConfigureOxQLStudio(options, Configuration));

            // Register web-api controller. Must be executed before creating swagger configuration
            var mvc = services.AddControllers(o =>
            {
                o.Filters.Add<RequestContextActionFilter>();
                o.Filters.Add<ValidationActionFilter>();
            });

            AddPackageControllers(mvc);
            MvcBuilder(mvc);

            // Global exception handling: a single problem+json contract for every error, built by an
            // ordered IExceptionHandler chain (OxSException -> known framework -> fallback) behind
            // UseExceptionHandler. Replaces the per-request MVC exception filter.
            services.AddProblemDetails();
            services.AddExceptionHandler<OxSExceptionHandler>();
            services.AddExceptionHandler<FrameworkExceptionHandler>();
            services.AddExceptionHandler<FallbackExceptionHandler>();

            // Suppress the [ApiController] automatic 400 so ValidationActionFilter produces the single
            // BadRequestException problem+json contract for model-state failures on every controller.
            // A filter that answers an invalid model state itself, through the factory of these
            // options (the query engine's, for a contract 1 body it does not hand to the binder),
            // gets that same document from it.
            services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o =>
            {
                o.SuppressModelStateInvalidFilter = true;
                o.InvalidModelStateResponseFactory = ValidationActionFilter.Answer;
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
            var basePath = $"/{ApiName}/{ApiVersion}";
            app.UsePathBase(basePath);

            if (env.IsDevelopment() || env.IsStaging() || env.EnvironmentName.ToLower() == "local")
            {
                app.UseDeveloperExceptionPage();
            }

            // Global exception handler chain (OxSException -> framework -> fallback) plus status-code
            // pages, so every error — including ones that never reach MVC — returns a consistent
            // problem+json body. In dev-facing environments the fallback declines, letting the
            // developer exception page above render the stack trace.
            app.UseExceptionHandler();
            app.UseStatusCodePages();

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
            });

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            // Add gRPC host validation middleware
            app.UseMiddleware<GrpcAuthenticationMiddleware>();

            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseMiddleware<PutJsonContextMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                MapHubs(endpoints);

                MapEndpoints(endpoints);

                // Map gRPC endpoints
                MapGrpcEndpoints(endpoints);

                // Map OxQL Studio UI (off in Production unless OxQL:Studio:Enabled says otherwise)
                if (OxQLStudioEnabled(Configuration, env))
                    endpoints.MapOxQLStudio();
            });
        }

        /// <summary>
        /// Will be called after controllers are created
        /// </summary>
        /// <param name="builder"><see cref="IMvcBuilder"/> instance</param>
        protected virtual void MvcBuilder(IMvcBuilder builder) { }

        /// <summary>
        /// Names this package's assembly as an application part, unless it is one already, so
        /// its controllers (the schema, addon definition, model definition and service
        /// definition routes, the internal query routes) are served. A host project of the web
        /// SDK names the assemblies it references as parts by itself; a project of the plain
        /// SDK names none, and the package's routes must not depend on that. The assembly is
        /// named once: the host's list of parts is what a service reads and changes in
        /// <see cref="MvcBuilder"/>, and it holds no entry twice.
        /// </summary>
        internal static void AddPackageControllers(IMvcBuilder builder)
        {
            var package = typeof(Bootstrap).Assembly;

            if (!builder.PartManager.ApplicationParts.OfType<Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart>().Any(part => part.Assembly == package))
                builder.AddApplicationPart(package);
        }

        /// <summary>
        /// Configures gRPC service endpoints on the specified endpoint route builder.
        /// </summary>
        /// <remarks>Override this method to customize the mapping of gRPC endpoints for your application.
        /// This method is typically called during application startup to define how gRPC services are exposed over
        /// HTTP.</remarks>
        /// <param name="builder">The endpoint route builder used to register gRPC endpoints. Cannot be null.</param>
        protected virtual void MapGrpcEndpoints(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder builder) { }

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
        /// <remarks>
        /// Runs once per host, when the mapper is created: while the host starts, before the Ox
        /// schema is built. An exception thrown here stops the start. MongoDB class maps and
        /// serializers registered here are in place for the schema build, but their place is
        /// <see cref="RegisterBsonClassMaps"/>.
        /// </remarks>
        /// <param name="mapperConfiguration">Mapper configuration instance. Add profiles to this mapping.</param>
        protected virtual void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration) { }

        /// <summary>
        /// The place for the service's MongoDB class maps (<c>BsonClassMap.RegisterClassMap</c>),
        /// serializers (<c>BsonSerializer.RegisterSerializer</c>) and conventions.
        /// </summary>
        /// <remarks>
        /// Called once per process for a startup type, however many hosts the process builds
        /// from it, so the registrations need no guard of their own. It runs during
        /// <see cref="ConfigureServices"/>, after the base registered its guid serializer and
        /// before <see cref="RegisterServices"/>, the mapper and the Ox schema build, which looks
        /// up the serializer of every entity and of every type reachable from one and freezes
        /// their class maps: a class map registered after that build throws or is not applied,
        /// and a stored subclass without one is missing from the schema's variants.
        /// <para>
        /// Inside the hook the order is: plain serializers first (the object serializer among
        /// them), class maps next, serializers that keep another serializer last (one for an
        /// interface or a discriminated type looks the object serializer up when it is created).
        /// The driver creates the default serializer of a type at its first lookup and refuses a
        /// registration for the type from then on; where a registration of the service meets
        /// that refusal, here or in <see cref="RegisterServices"/>,
        /// <see cref="RegisterMapperProfiles"/> or <see cref="ConfigureOxSchema"/>, the exception
        /// names this order.
        /// </para>
        /// </remarks>
        protected virtual void RegisterBsonClassMaps() { }

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
        /// Configures the specified gRPC service options before the service is started.
        /// </summary>
        /// <remarks>Override this method to customize gRPC service behavior by modifying the provided
        /// options before the service is initialized.</remarks>
        /// <param name="options">The options to configure for the gRPC service. Cannot be null.</param>
        protected virtual void ConfigureGrpc(GrpcServiceOptions options)
        {
            options.EnableDetailedErrors = true;

            // Configure gRPC security for localhost and service-name domain restrictions
            options.Interceptors.Add<GrpcSecurityInterceptor>();
            options.Interceptors.Add<GrpcHeaderContextInterceptor>();
        }

        /// <summary>
        /// Method that should return all controllers that are used to build model definitions
        /// </summary>
        /// <returns></returns>
        protected virtual IList<Type> ConfigureModelDefinitions() { return new List<Type>(); }

        /// <summary>
        /// Adjusts the inputs of the schema document beyond what the host already declares, for
        /// example to publish the ids an entity retired when it was renamed.
        /// </summary>
        protected virtual void ConfigureOxSchema(OxSchemaOptionsBuilder schema) { }

        /// <summary>
        /// Calls <see cref="RegisterBsonClassMaps"/> unless this process already called it for
        /// this startup type. A hook that threw is not run a second time over registrations it
        /// already made: every later call for the type throws what it threw.
        /// </summary>
        internal void EnsureBsonClassMaps()
        {
            lock (ClassMapsRegistered)
            {
                if (ClassMapsRegistered.TryGetValue(GetType(), out var thrown))
                {
                    thrown?.Throw();

                    return;
                }

                try
                {
                    SerializerRegistrationOrder.Explain(nameof(RegisterBsonClassMaps), RegisterBsonClassMaps);
                    ClassMapsRegistered[GetType()] = null;
                }
                catch (Exception exception)
                {
                    ClassMapsRegistered[GetType()] = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);

                    throw;
                }
            }
        }

        /// <summary>The startup types whose hook ran in this process, each with what the hook threw, if it did.</summary>
        private static readonly Dictionary<Type, System.Runtime.ExceptionServices.ExceptionDispatchInfo?> ClassMapsRegistered = [];

        /// <summary>
        /// Creates the mapper from <see cref="RegisterMapperProfiles"/>, for a host: every host
        /// gets a mapper of its own, so the profiles run for each. One run at a time in a
        /// process, whoever starts it, a host or <see cref="EnsureMapperProfiles"/>: what a
        /// service registers beside its profiles is the process's.
        /// </summary>
        internal IMapper CreateMapper()
        {
            lock (MapperProfilesRegistered)
            {
                try
                {
                    var mapper = SerializerRegistrationOrder.Explain(nameof(RegisterMapperProfiles), () => new MapperConfiguration(RegisterMapperProfiles).CreateMapper());

                    MapperProfilesRegistered[GetType()] = null;

                    return mapper;
                }
                catch (Exception exception)
                {
                    MapperProfilesRegistered[GetType()] = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);

                    throw;
                }
            }
        }

        /// <summary>
        /// Runs <see cref="RegisterMapperProfiles"/> unless this process already ran it for this
        /// startup type, in a host or here: what <see cref="OxSchemaCheck"/> needs of the mapper
        /// is the class maps a service registers beside its profiles, which are the process's.
        /// Profiles that threw are not run a second time over registrations they already made:
        /// every later call for the type throws what they threw.
        /// </summary>
        internal void EnsureMapperProfiles()
        {
            // Held while the profiles run, so a concurrent check of the same type waits for the
            // registrations instead of building its schema beside them.
            lock (MapperProfilesRegistered)
            {
                if (MapperProfilesRegistered.TryGetValue(GetType(), out var thrown))
                {
                    thrown?.Throw();

                    return;
                }

                CreateMapper();
            }
        }

        /// <summary>The startup types whose mapper profiles ran in this process, each with what the last run threw, if it did.</summary>
        private static readonly Dictionary<Type, System.Runtime.ExceptionServices.ExceptionDispatchInfo?> MapperProfilesRegistered = [];

        /// <summary>
        /// Fills the schema's inputs from what the service declares and lets
        /// <see cref="ConfigureOxSchema"/> adjust them: the one definition of what a host hands
        /// the schema build, for the host itself and for <see cref="OxSchemaCheck"/>.
        /// </summary>
        internal void FillOxSchema(OxSchemaOptionsBuilder schema, IReadOnlyList<Assembly> typeAssemblies)
        {
            schema.ServiceName = ServiceName;
            schema.ApiName = ApiName;
            schema.ApiVersion = ApiVersion;
            schema.TypeAssemblies = typeAssemblies;
            schema.NoEntitiesDeclared = noEntitiesDeclared;
            schema.ControllerTypes = ConfigureModelDefinitions().ToArray();
            schema.EnvironmentName = CurrentEnvironment.EnvironmentName;

            // Whoever requires authorization for the document is heard: the configuration, or a
            // registration of the service that came before the host's. ConfigureOxSchema decides last.
            schema.RequireAuthorization |= Configuration.GetValue<bool>("OxSchema:RequireAuthorization");

            SerializerRegistrationOrder.Explain(nameof(ConfigureOxSchema), () => ConfigureOxSchema(schema));
        }

        /// <summary>
        /// The assemblies <see cref="GetOxQLTypeAssemblies"/> names, each once. An empty list is a
        /// service that declares it has no entities, which the schema is told
        /// (<see cref="FillOxSchema"/>); no list at all (null) is not.
        /// </summary>
        internal Assembly[] OxQLTypeAssemblies()
        {
            var named = GetOxQLTypeAssemblies();

            noEntitiesDeclared = named is { Count: 0 };

            return (named ?? new List<Assembly>()).Distinct().ToArray();
        }

        /// <summary>Whether the last call of <see cref="OxQLTypeAssemblies"/> found an empty list.</summary>
        private bool noEntitiesDeclared;

        /// <summary>
        /// Calls <see cref="RegisterServices"/>, for a host: every host registers its services.
        /// One run at a time in a process, whoever starts it, a host or
        /// <see cref="EnsureRegisterServices"/>: what a service registers beside its services
        /// (a class map, a serializer) is the process's.
        /// </summary>
        internal void RegisterServicesInto(IServiceCollection services)
        {
            lock (ServicesRegistered)
            {
                try
                {
                    SerializerRegistrationOrder.Explain(nameof(RegisterServices), () => RegisterServices(services));
                    ServicesRegistered[GetType()] = null;
                }
                catch (Exception exception)
                {
                    ServicesRegistered[GetType()] = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);

                    throw;
                }
            }
        }

        /// <summary>
        /// Runs <see cref="RegisterServices"/> into a collection that is thrown away, unless this
        /// process already ran it for this startup type, in a host or here: what
        /// <see cref="OxSchemaCheck"/> needs of it is the class maps and serializers a service
        /// registers there, which are the process's and bear no second registration. A run
        /// that threw is not repeated over registrations it already made: every later call for
        /// the type throws what it threw.
        /// </summary>
        internal void EnsureRegisterServices()
        {
            lock (ServicesRegistered)
            {
                if (ServicesRegistered.TryGetValue(GetType(), out var thrown))
                {
                    thrown?.Throw();

                    return;
                }

                RegisterServicesInto(new ServiceCollection());
            }
        }

        /// <summary>The startup types whose <see cref="RegisterServices"/> ran in this process, each with what the last run threw, if it did.</summary>
        private static readonly Dictionary<Type, System.Runtime.ExceptionServices.ExceptionDispatchInfo?> ServicesRegistered = [];

        /// <summary>
        /// Method that should return all assemblies that contains OxQL types. This is used for scanning and registering the types in the OxQL system.
        /// A service without entities returns an empty list: the schema then describes no types and reports nothing.
        /// </summary>
        /// <returns>List of assemblies containing OxQL types</returns>
        protected abstract IList<Assembly> GetOxQLTypeAssemblies();

        /// <summary>
        /// Method for configuring organization settings. Return null to disable settings.
        /// </summary>
        /// <returns>Settings configuration action or null</returns>
        protected virtual Action<IOrganizationSettingsBuilder>? ConfigureOrganizationSettings() { return null; }

        /// <summary>
        /// Whether the OxQL Studio console is served: <c>OxQL:Studio:Enabled</c>, which defaults to on
        /// in every environment but Production. The console is an anonymous developer page on the
        /// API's origin that loads its editor from a CDN and keeps a pasted bearer in the browser, so
        /// a production host serves it only when configured to.
        /// </summary>
        internal static bool OxQLStudioEnabled(IConfiguration configuration, IHostEnvironment environment) =>
            configuration.GetValue("OxQL:Studio:Enabled", !environment.IsProduction());

        /// <summary>
        /// The OxQL Studio console's options. Every path is relative to the path base
        /// (<c>/{ApiName}/{ApiVersion}</c>), which the console prefixes itself: the console at
        /// <c>{pathBase}/oxql</c>, beside the API it calls (<c>{pathBase}/oxql/query</c>, …) and the
        /// schema it reads (<c>{pathBase}/schema</c>). Its Explain button follows the engine's
        /// <c>OxQL:Explain:Enabled</c>, which is on unless configured off.
        /// </summary>
        internal static void ConfigureOxQLStudio(OxQLStudioOptions options, IConfiguration configuration)
        {
            options.RoutePath = "/oxql";
            options.ApiBasePath = "/oxql";
            options.SchemaBasePath = "/schema";
            options.Title = "OxQL Studio";
            options.EnableExplain = configuration.GetValue("OxQL:Explain:Enabled", true);
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
        /// Gets the first segment of the service's base path, e.g. <c>vehicle-api</c>.
        /// </summary>
        /// <remarks>
        /// One definition of the convention, because two places need it: the path base and the
        /// <c>api</c> member of the schema document, which is a published claim about where this
        /// service answers.
        /// </remarks>
        protected string ApiName => $"{ServiceName.ToLowerInvariant()}-api";

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


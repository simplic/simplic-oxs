using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Data;
using Simplic.OxS.InternalClient;
using Simplic.OxS.MessageBroker;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.Server.Services;

namespace Simplic.OxS.Server;

/// <summary>
/// Feature that shall be used in <see cref="BootstrapLite"/>.
/// </summary>
public enum BootstrapLiteFeature
{
    /// <summary>
    /// Use logging.
    /// </summary>
    Logging,

    /// <summary>
    /// Use Redis caching.
    /// </summary>
    Redis,

    /// <summary>
    /// Use MongoDB.
    /// </summary>
    MongoDb,

    /// <summary>
    /// Use RabbitMQ.
    /// </summary>
    RabbitMq,
}

/// <summary>
/// Lighter than <see cref="Bootstrap"/>.<br/>
/// Contains the minimum that some sort of server/service should have.<br/>
/// A good use case is if the server is something else than asp-net which would render <see cref="Bootstrap"/> unusable.
/// </summary>
public abstract class BootstrapLite
{
    private readonly HashSet<BootstrapLiteFeature> features = [];

    /// <summary>
    /// Initialize web api and configure services. Will be called from the host-builder.
    /// </summary>
    /// <param name="configuration">Configuration service</param>
    /// <param name="features">Features to use</param>
    public BootstrapLite(IConfiguration configuration, IEnumerable<BootstrapLiteFeature>? features = null)
    {
        Configuration = configuration;

        if (features is not null)
            this.features = features.ToHashSet();
    }

    /// <summary>
    /// Adds features.
    /// </summary>
    /// <param name="features"></param>
    public void AddFeatures(params BootstrapLiteFeature[] features)
    {
        foreach (var feature in features)
        {
            this.features.Add(feature);
        }
    }

    /// <summary>
    /// This method gets called by the runtime. Use this method to add services to the container.
    /// </summary>
    /// <param name="services">Service collection</param>
    public virtual void ConfigureServices(IServiceCollection services)
    {
        if (features.Contains(BootstrapLiteFeature.Logging))
        {
            // Add logging and tracing systems
            services.AddLoggingAndMetricTracing(Configuration, ServiceName);
        }

        if (features.Contains(BootstrapLiteFeature.Redis))
        {
            // Add Redis caching
            services.AddRedisCaching(Configuration, out _);
        }

        if (features.Contains(BootstrapLiteFeature.MongoDb))
        {
            // Add MongoDb context and bind configuration
            services.AddMongoDb(Configuration);
        }

        if (features.Contains(BootstrapLiteFeature.RabbitMq))
        {
            // Add RabbitMq context and bind configuration
            services.AddRabbitMQ(Configuration, ConfigureEndpointConventions);
        }

        // Register custom services
        RegisterServices(services);

        // Create mapper profiles and register mapper
        var mapperConfig = new MapperConfiguration(RegisterMapperProfiles);

        var mapper = mapperConfig.CreateMapper();
        services.AddSingleton(mapper);

        services.AddTransient<IMapService, MapService>();

        // Add internal services
        services.AddScoped<IRequestContext, RequestContext>();
        services.AddScoped<IInternalClient, InternalClientBase>();
    }


    /// <summary>
    /// Method for custom profile registrations. (AutoMapper)
    /// </summary>
    /// <param name="mapperConfiguration">Mapper configuration instance. Add profiles to this mapping.</param>
    protected virtual void RegisterMapperProfiles(IMapperConfigurationExpression mapperConfiguration)
    {
    }

    /// <summary>
    /// Method for custom endpoint registration (MassTransit/RabbitMq). Register commands in this method.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="settings">MessageBroker settings</param>
    protected virtual void ConfigureEndpointConventions(IServiceCollection services, MessageBrokerSettings settings)
    {
    }


    /// <summary>
    /// Will be called for registering custom services.
    /// </summary>
    /// <param name="services">Service collection for adding additional services</param>
    protected abstract void RegisterServices(IServiceCollection services);

    /// <summary>
    /// Gets the current service name.<br/>
    /// This will not contain 'Simplic.OxS'.<br/>
    /// Used for logging and tracing.
    /// </summary>
    protected abstract string ServiceName { get; }

    /// <summary>
    /// Gets the current configuration service.
    /// </summary>
    public IConfiguration Configuration { get; }
}
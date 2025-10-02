using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simplic.OxS.Settings.Abstractions;
using Simplic.OxS.Settings.Organization;
using Simplic.OxS.Settings.Repository;
using System.Text.Json;

namespace Simplic.OxS.Server.Extensions;

/// <summary>
/// Extension methods for configuring organization settings in Bootstrap
/// </summary>
public static class OrganizationSettingsExtensions
{
    /// <summary>
    /// Add organization settings support with MongoDB storage and distributed caching
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action for settings</param>
    /// <param name="serviceName">Name of the microservice for cache key scoping</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddOrganizationSettingsWithMongo(
        this IServiceCollection services,
        Action<IOrganizationSettingsBuilder> configure,
        string serviceName)
    {
        // Register MongoDB repository implementation
        services.AddScoped<IOrganizationSettingRepository, OrganizationSettingRepository>();

        // Register JSON serializer options for settings (if not already registered)
        services.TryAddSingleton(provider =>
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        });

        // Register service name for cache scoping
        services.AddSingleton(new OrganizationSettingsConfiguration { ServiceName = serviceName });

        // Register core services
        var registry = new OrganizationSettingsRegistry();
        services.AddSingleton(registry);
        services.AddScoped<IOrganizationSettingsProvider, OrganizationSettingsProvider>();

        // Configure settings declaratively using the builder
        var builder = new OrganizationSettingsBuilder(registry);
        configure(builder);

        return services;
    }
}

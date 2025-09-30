using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Settings.Organization;
using Simplic.OxS.Settings.Repository;

namespace Simplic.OxS.Server.Extensions;

/// <summary>
/// Extension methods for configuring organization settings in Bootstrap
/// </summary>
public static class OrganizationSettingsExtensions
{
    /// <summary>
    /// Add organization settings support with MongoDB storage
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action for settings</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddOrganizationSettingsWithMongo(
        this IServiceCollection services,
        Action<IOrganizationSettingsBuilder> configure)
    {
        // Register MongoDB repository implementation
        services.AddScoped<IOrganizationSettingRepository, OrganizationSettingRepository>();

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

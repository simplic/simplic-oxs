using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.Data.MongoDB;
using Simplic.OxS.Data.MongoDB.Abac;
using Simplic.OxS.Server.Abac;
using Simplic.OxS.Server.Abac.Sync;

namespace Simplic.OxS.Server.Extensions;

/// <summary>
/// Extension methods for registering the runtime ABAC services with the DI container.
/// </summary>
public static class AbacRuntimeExtension
{
    /// <summary>
    /// Registers the runtime ABAC services. Options are bound from the configuration section <c>Abac</c>
    /// (<see cref="AbacRuntimeOptions"/>) of the <see cref="IConfiguration"/> registered in DI.
    /// <list type="bullet">
    ///   <item><see cref="IAbacSubjectFactory"/> → <see cref="AbacSubjectFactory"/> (scoped; <c>Id</c>/<c>OId</c> from claims, roles/teams via
    ///   <see cref="IAbacSubjectAttributeProvider"/> → <see cref="AbacSubjectAttributeProvider"/> (scoped, L1 + Redis, TTL <c>Abac:Subject:Ttl</c>)
    ///   on top of <see cref="IAbacSubjectAttributeClient"/> → <see cref="AbacSubjectAttributeHttpClient"/> (typed HttpClient))</item>
    ///   <item><see cref="IAbacPolicyReader"/> → <see cref="AbacPolicyReader"/> (scoped; Redis → local store → pull → unavailable)</item>
    ///   <item><see cref="IAbacEvaluator"/> → <see cref="AbacEvaluator"/> (scoped; L1 cache via <c>IMemoryCache</c>)</item>
    ///   <item><see cref="IAbacRuleSetClient"/> → <see cref="AbacRuleSetHttpClient"/> (typed HttpClient), <see cref="IAbacRuleSyncService"/> → <see cref="AbacRuleSyncService"/> (scoped)</item>
    ///   <item><see cref="IAbacRuleStore"/> → <see cref="MongoAbacRuleStore"/> and the <see cref="AbacRuleSyncBackgroundService"/> — only when <see cref="IMongoContext"/> is registered</item>
    ///   <item><see cref="IAbacReadFilterProvider"/> → <see cref="AbacReadFilterProvider"/>, <see cref="AbacBusGuard"/>, <see cref="IAbacSignalRFilter"/> → <see cref="AbacSignalRBroadcastFilter"/> (scoped)</item>
    ///   <item><see cref="AbacAccessDeniedExceptionFilter"/> as global MVC filter (→ 403)</item>
    /// </list>
    /// The <see cref="AbacPolicySetActivatedConsumer"/> must be activated by the service (see its documentation).
    /// <para>Call this from your Bootstrap's <c>RegisterServices</c> override: <c>services.AddAbacRuntime();</c></para>
    /// </summary>
    public static IServiceCollection AddAbacRuntime(this IServiceCollection services)
    {
        services.AddOptions<AbacRuntimeOptions>()
            .BindConfiguration(AbacRuntimeOptions.SectionName)
            .Configure<IConfiguration>(ApplyScalarActionPrefixes);

        return services.AddAbacRuntimeCore(registerStore: services.Any(d => d.ServiceType == typeof(IMongoContext)));
    }

    /// <summary>
    /// Registers the runtime ABAC services (see <see cref="AddAbacRuntime(IServiceCollection)"/>) binding
    /// <see cref="AbacRuntimeOptions"/> from <paramref name="configuration"/>. The local rule store is registered
    /// when <see cref="IMongoContext"/> is registered and <c>MongoDB:ConnectionString</c> is configured.
    /// </summary>
    public static IServiceCollection AddAbacRuntime(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AbacRuntimeOptions>()
            .Bind(configuration.GetSection(AbacRuntimeOptions.SectionName))
            .Configure(o => ApplyScalarActionPrefixes(o, configuration));

        var registerStore = services.Any(d => d.ServiceType == typeof(IMongoContext))
            && !string.IsNullOrWhiteSpace(configuration["MongoDB:ConnectionString"]);

        return services.AddAbacRuntimeCore(registerStore);
    }

    private static IServiceCollection AddAbacRuntimeCore(this IServiceCollection services, bool registerStore)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddMetrics();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<AbacRuntimeMetrics>();
        services.TryAddSingleton<AbacSyncBackoff>();

        services.TryAddScoped<IAbacSubjectFactory, AbacSubjectFactory>();
        services.AddHttpClient<IAbacSubjectAttributeClient, AbacSubjectAttributeHttpClient>();
        services.TryAddScoped<IAbacSubjectAttributeProvider, AbacSubjectAttributeProvider>();
        services.TryAddScoped<IAbacPolicyReader, AbacPolicyReader>();
        services.TryAddScoped<IAbacEvaluator, AbacEvaluator>();

        services.AddHttpClient<IAbacRuleSetClient, AbacRuleSetHttpClient>();
        services.TryAddScoped<IAbacRuleSyncService, AbacRuleSyncService>();

        if (registerStore && !services.Any(d => d.ServiceType == typeof(IAbacRuleStore)))
        {
            services.AddScoped<IAbacRuleStore, MongoAbacRuleStore>();
            services.AddHostedService<AbacRuleSyncBackgroundService>();
        }

        services.TryAddScoped<IAbacReadFilterProvider, AbacReadFilterProvider>();
        services.TryAddScoped<AbacBusGuard>();
        services.TryAddScoped<IAbacSignalRFilter, AbacSignalRBroadcastFilter>();

        services.Configure<MvcOptions>(o =>
        {
            if (!o.Filters.OfType<TypeFilterAttribute>().Any(f => f.ImplementationType == typeof(AbacAccessDeniedExceptionFilter)))
                o.Filters.Add<AbacAccessDeniedExceptionFilter>();
        });

        return services;
    }

    /// <summary>Supports <c>Abac:Sync:ActionPrefixes</c> given as a comma-separated scalar string.</summary>
    private static void ApplyScalarActionPrefixes(AbacRuntimeOptions options, IConfiguration configuration)
    {
        var scalar = configuration[$"{AbacRuntimeOptions.SectionName}:Sync:ActionPrefixes"];
        if (!string.IsNullOrWhiteSpace(scalar))
            options.Sync.ActionPrefixes = scalar.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

using Hangfire;
using Hangfire.Mongo;
using Hangfire.Mongo.Migration.Strategies;
using Hangfire.Mongo.Migration.Strategies.Backup;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Data.MongoDB;

namespace Simplic.OxS.Scheduler
{
    /// <summary>
    /// MongoDb service extension
    /// </summary>
    public static class SchedulerExtension
    {
        public static IServiceCollection AddJobScheduler(this IServiceCollection services, IConfiguration configuration, string serviceName)
        {
            services.Configure<ConnectionSettings>(options => configuration.GetSection("MongoDB").Bind(options));

            var settings = configuration.GetSection("MongoDB")?.Get<ConnectionSettings>();

            if (settings != null)
            {
                Console.WriteLine(" > Add scheduler system");

                // Add Hangfire services
                services.AddHangfire(configuration => configuration
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseMongoStorage(settings.ConnectionString, settings.Database, new MongoStorageOptions
                    {
                        MigrationOptions = new MongoMigrationOptions
                        {
                            MigrationStrategy = new MigrateMongoMigrationStrategy(),
                            BackupStrategy = new CollectionMongoBackupStrategy()
                        },
                        Prefix = $"hangfire.{serviceName.ToLower()}",
                        CheckConnection = true
                    })
                );

                // Add the processing server as IHostedService
                services.AddHangfireServer(serverOptions =>
                {
                    serverOptions.ServerName = $"Hangfire.Mongo server - {serviceName.ToLower()}";
                    serverOptions.Queues = new[] { serviceName.ToLower() };
                });

                // Register the failed-job cleanup so it is available for DI in MapScheduler.
                services.AddScoped<FailedJobCleanup>();
            }

            return services;
        }

        public static IEndpointRouteBuilder MapScheduler(this IEndpointRouteBuilder endpoints, string serviceName)
        {
            var logger = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(SchedulerExtension));

            try
            {
                var recurringJobManager = endpoints.ServiceProvider.GetService<IRecurringJobManager>();
                if (recurringJobManager != null)
                {
                    logger.LogInformation("Add cleanup job: cleanup-failed-jobs");

                    // This adds one cleanup job. It does not recreate or modify other jobs.
                    recurringJobManager.AddOrUpdate<FailedJobCleanup>(
                        recurringJobId: "cleanup-failed-jobs",
                        methodCall: cleanup => cleanup.DeleteFailedJobsOlderThan(
                            retentionDays: 7,
                            batchSize: 500),
                        cronExpression: Cron.Daily);
                }
                else
                {
                    logger.LogWarning("Could not add cleanup job: IRecurringJobManager is not available (Hangfire storage not configured)");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not add cleanup job");
            }

            try
            {
                endpoints.MapHangfireDashboard(new DashboardOptions
                {
                    DashboardTitle = $"Hangfire - {serviceName}",
                    AppPath = $"/{serviceName}-api/v1/hangfire",
                    DisplayStorageConnectionString = false,
                    Authorization = new[]
                    {
                        new DashboardAuthorizationFilter()
                    }
                });

                logger.LogInformation("Hangfire dashboard mapped successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not map Hangfire dashboard");
            }

            return endpoints;
        }
    }
}

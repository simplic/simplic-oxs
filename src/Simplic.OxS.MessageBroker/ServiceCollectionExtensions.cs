using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.MessageBroker.Filter;
using System.Reflection;

namespace Simplic.OxS.MessageBroker
{
    /// <summary>
    /// Service collection extension
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Initialize mass transit
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Configuration instance</param>
        /// <param name="additionalConfiguration">Delegate for invoking masstransit pipeline creation</param>
        /// <returns>Service collection instance</returns>
        public static IServiceCollection InitializeMassTransit(this IServiceCollection services, IConfiguration configuration, Action<IBusRegistrationConfigurator>? additionalConfiguration = null)
        {
            var consumerTypes = Assembly.GetEntryAssembly().GetTypes()
                .Where(t => typeof(IConsumer).IsAssignableFrom(t))
                .ToList();

            Console.WriteLine($"Consumers count: {consumerTypes.Count}");

            services.AddMassTransit(configurator =>
            {
                var registeredConsumer = new List<Type>();

                Console.WriteLine(" > Add consumer");
                foreach (var consumer in consumerTypes)
                {
                    if (consumer.GetCustomAttributes().Any(x => x.GetType() == typeof(QueueAttribute))
                        || consumer.GetCustomAttributes().Any(x => x.GetType() == typeof(NoQueueAttribute)))
                    {
                        var attribute = consumer.GetCustomAttributes()
                                                .FirstOrDefault();

                        Console.WriteLine($" Consumer found {consumer.FullName}");
                        services.AddScoped(consumer);
                        registeredConsumer.Add(consumer);
                    }
                }

                additionalConfiguration?.Invoke(configurator);

                configurator.UsingRabbitMq((context, cfg) =>
                {
                    cfg.InitializeHost(configuration);

                    if (consumerTypes.Any())
                    {
                        var consumers = new Dictionary<string, Type>();
                        var queuelessConsumer = new List<Type>();

                        foreach (var consumerType in registeredConsumer)
                        {

                            var attributes = consumerType.GetCustomAttributes(typeof(QueueAttribute), true);
                            if (attributes.Any())
                            {
                                var queueName = ((QueueAttribute)attributes[0]).Name;
                                consumers.Add(queueName, consumerType);
                            }

                            attributes = consumerType.GetCustomAttributes(typeof(NoQueueAttribute), true);
                            if (attributes.Any())
                                queuelessConsumer.Add(consumerType);
                        }

                        Console.WriteLine($"Consumers found: {consumers.Count}");
                        Console.WriteLine($"Queueless consumers found: {queuelessConsumer.Count}");

                        foreach (var consumer in consumers)
                        {
                            cfg.ReceiveEndpoint(consumer.Key, ec =>
                            {
                                // Inject pipeline and set request context
                                ec.UseMessageScope(context);
                                ec.UseConsumeFilter(typeof(ConsumeContextFilter<>), context);
                                ec.Consumer(consumer.Value, x =>
                                {
                                    return context.GetRequiredService(x);
                                });
                            });
                        }

                        // Register event-consumer without the need of having a queue
                        foreach (var consumer in queuelessConsumer)
                        {
                            cfg.ReceiveEndpoint(ec =>
                            {
                                // Inject pipeline and set request context
                                ec.UseMessageScope(context);
                                ec.UseConsumeFilter(typeof(ConsumeContextFilter<>), context);
                                ec.Consumer(consumer, x => { return context.GetRequiredService(x); });
                            });
                        }

                        cfg.ConfigureSend(sendPipeConfigurator => sendPipeConfigurator.UseExecute(ctx =>
                        {
                            ctx.Headers.Set(MassTransitHeaders.TenantId, GetTenantId(context));
                            ctx.Headers.Set(MassTransitHeaders.UserId, GetUserId(context));

                            ctx.CorrelationId = GetCorrelationId(context);
                        }));

                        cfg.ConfigurePublish(publishPipeConfigurator => publishPipeConfigurator.UseExecute(ctx =>
                        {
                            ctx.Headers.Set(MassTransitHeaders.TenantId, GetTenantId(context));
                            ctx.Headers.Set(MassTransitHeaders.UserId, GetUserId(context));

                            ctx.CorrelationId = GetCorrelationId(context);
                        }));
                    }
                });
            });

            return services;
        }

        /// <summary>
        /// Try to get the tenant id from the request context
        /// </summary>
        /// <param name="context">Current bus context</param>
        /// <returns>Tenant id</returns>
        private static string? GetTenantId(IBusRegistrationContext context)
        {
            var httpContextAccessor = context.GetRequiredService<IRequestContext>();
            return httpContextAccessor?.TenantId?.ToString();
        }

        /// <summary>
        /// Try to get the user id from the request context
        /// </summary>
        /// <param name="context">Current bus context</param>
        /// <returns>User id</returns>
        private static string? GetUserId(IBusRegistrationContext context)
        {
            var httpContextAccessor = context.GetRequiredService<IRequestContext>();
            return httpContextAccessor?.UserId?.ToString();
        }

        /// <summary>
        /// Try to get the correlation id from the request context
        /// </summary>
        /// <param name="context">Current bus context</param>
        /// <returns>Correlation id</returns>
        private static Guid? GetCorrelationId(IBusRegistrationContext context)
        {
            var httpContextAccessor = context.GetRequiredService<IRequestContext>();
            return httpContextAccessor?.CorrelationId ?? Guid.NewGuid();
        }
    }
}
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
                Console.WriteLine(" > Add consumer");
                foreach (var consumerType in consumerTypes)
                {
                    Console.WriteLine($"  {consumerType.Name}");
                    configurator.AddConsumer(consumerType);
                }

                additionalConfiguration?.Invoke(configurator);

                configurator.UsingRabbitMq((context, cfg) =>
                {
                    cfg.InitializeHost(configuration);

                    if (consumerTypes.Any())
                    {
                        var consumers = new Dictionary<string, Type>();

                        foreach (var type in consumerTypes)
                        {
                            var attributes = type.GetCustomAttributes(typeof(QueueAttribute), true);
                            if (attributes.Any())
                            {
                                var queueName = ((QueueAttribute)attributes[0]).Name;

                                Console.WriteLine($"Add receiver: {queueName}");

                                consumers.Add(queueName, type);
                            }
                            else
                            {
                                Console.WriteLine($"Consumer {type.Name} does not consume any queue. Please use {nameof(QueueAttribute)}.");
                            }
                        }

                        foreach (var consumer in consumers)
                        {
                            cfg.ReceiveEndpoint(consumer.Key, ep =>
                            {
                                // Inject pipeline and set request context
                                ep.UseConsumeFilter(typeof(ConsumeContextFilter<>), context);
                                ep.ConfigureConsumer(context, consumer.Value);
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
            var httpContextAccessor = context.GetService<IRequestContext>();
            return httpContextAccessor?.TenantId?.ToString();
        }

        /// <summary>
        /// Try to get the user id from the request context
        /// </summary>
        /// <param name="context">Current bus context</param>
        /// <returns>User id</returns>
        private static string? GetUserId(IBusRegistrationContext context)
        {
            var httpContextAccessor = context.GetService<IRequestContext>();
            return httpContextAccessor?.UserId?.ToString();
        }

        /// <summary>
        /// Try to get the correlation id from the request context
        /// </summary>
        /// <param name="context">Current bus context</param>
        /// <returns>Correlation id</returns>
        private static Guid? GetCorrelationId(IBusRegistrationContext context)
        {
            var httpContextAccessor = context.GetService<IRequestContext>();
            return httpContextAccessor?.CorrelationId ?? Guid.NewGuid();
        }
    }
}
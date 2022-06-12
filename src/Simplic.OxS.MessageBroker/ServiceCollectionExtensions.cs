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
                foreach (var consumer in consumerTypes)
                {
                    if (consumer.GetCustomAttributes().Any(x => x.GetType() == typeof(ConsumerAttribute)))
                    {
                        var attribute = consumer.GetCustomAttributes()
                                                .OfType<ConsumerAttribute>()
                                                .FirstOrDefault();

                        if (attribute == null)
                            continue;

                        if (attribute.ConsumerDefinition == null)
                            configurator.AddConsumer(consumer);
                        else
                            configurator.AddConsumer(consumer, attribute.ConsumerDefinition);

                        Console.WriteLine($" Consumer added {consumer.FullName} / Definition type: {attribute.ConsumerDefinition}");
                    }
                }

                additionalConfiguration?.Invoke(configurator);

                configurator.UsingRabbitMq((context, cfg) =>
                {
                    cfg.InitializeHost(configuration);

                    cfg.UseSendFilter(typeof(SendUserHeaderFilter<>), context);
                    cfg.UsePublishFilter(typeof(PublishUserHeaderFilter<>), context);
                    cfg.UseConsumeFilter(typeof(ConsumeContextFilter<>), context);

                    cfg.ConfigureEndpoints(context);
                });
            });

            return services;
        }
    }
}
using MassTransit;
using MassTransit.ExtensionsDependencyInjectionIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Simplic.OxS.MessageBroker
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection InitializeMassTransit(this IServiceCollection services, IConfiguration configuration, Action<IServiceCollectionBusConfigurator> additionalConfiguration = null)
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
                                Console.WriteLine(
                                    $"Consumer {type.Name} does not consume any queue. Please use {nameof(QueueAttribute)}.");
                            }
                        }

                        foreach (var consumer in consumers)
                        {
                            cfg.ReceiveEndpoint(consumer.Key,
                                ep => { ep.ConfigureConsumer(context, consumer.Value); });
                        }
                    }
                });
            });

            return services;
        }
    }
}
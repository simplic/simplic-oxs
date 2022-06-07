using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Data.MongoDB;
using Simplic.OxS.MessageBroker;

namespace Simplic.OxS.Server.Extensions
{
    /// <summary>
    /// Messagebroker extension methods
    /// </summary>
    internal static class RabbitMqExtension
    {
        /// <summary>
        /// Add rabbitmq/masstransit
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Actual configuration instance</param>
        /// <param name="configure">Configure rabbitmq</param>
        /// <returns>Service collection</returns>
        internal static IServiceCollection AddRabbitMQ(this IServiceCollection services, IConfiguration configuration, Action<IServiceCollection, MessageBrokerSettings> configure)
        {
            // Initialize broker system
            var rabbitMQSettings = configuration.GetSection("rabbitMQ").Get<MessageBrokerSettings>();
            if (rabbitMQSettings != null && !string.IsNullOrWhiteSpace(rabbitMQSettings.Host))
            {
                Console.WriteLine($" > Add MassTransit context: {rabbitMQSettings.Host}@{rabbitMQSettings.UserName}");
                services.InitializeMassTransit(configuration, null);

                configure(services, rabbitMQSettings);
            }
            else
            {
                Console.WriteLine(" > NO MassTransit context found.");
            }

            return services;
        }
    }
}

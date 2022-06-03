using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Data.MongoDB;
using Simplic.OxS.MessageBroker;

namespace Simplic.OxS.Server.Extensions
{
    internal static class RabbitMqExtension
    {
        internal static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration configuration, Action<IServiceCollection, MessageBrokerSettings> configure)
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

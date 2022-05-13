using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.Configuration;

namespace Simplic.OxS.MessageBroker
{
    public static class MassTransitExtensions
    {
        public static void InitializeHost(this IRabbitMqBusFactoryConfigurator rabbitMQConfigurator, IConfiguration configuration)
        {
            var rabbitMQSettings = configuration.GetSection("RabbitMQ").Get<MessageBrokerSettings>();
            System.Console.WriteLine($"Initialize RabbitMQ host: {rabbitMQSettings?.Host}");

            rabbitMQConfigurator.Host(rabbitMQSettings.Host, host =>
            {
                host.Username(rabbitMQSettings.UserName);
                host.Password(rabbitMQSettings.Password);
            });
        }
    }
}

using MassTransit;
using Microsoft.Extensions.Configuration;

namespace Simplic.OxS.MessageBroker
{
    /// <summary>
    /// Mass transit extensions
    /// </summary>
    internal static class MassTransitExtensions
    {
        /// <summary>
        /// Initialize masstransit host
        /// </summary>
        /// <param name="rabbitMQConfigurator">Configurator instance</param>
        /// <param name="configuration">Asp.net core configuration instance</param>
        internal static void InitializeHost(this IRabbitMqBusFactoryConfigurator rabbitMQConfigurator, IConfiguration configuration)
        {
            var rabbitMQSettings = configuration.GetSection("RabbitMQ").Get<MessageBrokerSettings>();
            System.Console.WriteLine($"Initialize RabbitMQ host: {rabbitMQSettings?.Host}");

            if(rabbitMQSettings?.Host == null)
                throw new Exception("No rabbitmq host found when trying to initialize rabbitmq host.");

            rabbitMQConfigurator.Host(rabbitMQSettings.Host, host =>
            {
                host.Username(rabbitMQSettings.UserName);
                host.Password(rabbitMQSettings.Password);
            });
        }
    }
}

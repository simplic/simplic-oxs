using MassTransit;
using MassTransit.ExtensionsDependencyInjectionIntegration;
using Microsoft.AspNetCore.Http;
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
                                Console.WriteLine($"Consumer {type.Name} does not consume any queue. Please use {nameof(QueueAttribute)}.");
                            }
                        }

                        foreach (var consumer in consumers)
                        {
                            cfg.ReceiveEndpoint(consumer.Key,
                                ep => { ep.ConfigureConsumer(context, consumer.Value); });
                        }

                        cfg.ConfigureSend(sendPipeConfigurator => sendPipeConfigurator.UseExecute(ctx =>
                        {
                            ctx.Headers.Set(MassTransitHeaders.OrganizationId, GetOrganizationId(context) ?? Guid.Empty.ToString());
                            ctx.Headers.Set(MassTransitHeaders.UserId, GetUserId(context) ?? Guid.Empty.ToString());
                        }));

                        cfg.ConfigurePublish(publishPipeConfigurator => publishPipeConfigurator.UseExecute(ctx =>
                        {
                            ctx.Headers.Set(MassTransitHeaders.OrganizationId, GetOrganizationId(context) ?? Guid.Empty.ToString());
                            ctx.Headers.Set(MassTransitHeaders.UserId, GetUserId(context) ?? Guid.Empty.ToString());
                        }));
                    }
                });
            });

            return services;
        }

        /// <summary>
        /// Try to get organizationId from user claims (it's available if a user is logged into any organization)
        /// </summary>
        /// <param name="context"></param>
        /// <returns>Organization Id</returns>
        private static string? GetOrganizationId(IBusRegistrationContext context)
        {
            var httpContextAccessor = context.GetService<IHttpContextAccessor>();

            if (httpContextAccessor == null)
                return null;

            var organizationId = httpContextAccessor.HttpContext.User.OrganizationId();

            return organizationId;
        }

        /// <summary>
        /// Try to get userId from user claims (it's available if a user is logged in)
        /// </summary>
        /// <param name="context"></param>
        /// <returns>User Id</returns>
        private static string? GetUserId(IBusRegistrationContext context)
        {
            var httpContextAccessor = context.GetService<IHttpContextAccessor>();

            if (httpContextAccessor == null)
                return null;

            var userId = httpContextAccessor.HttpContext.User.Id();

            return userId;
        }
    }
}
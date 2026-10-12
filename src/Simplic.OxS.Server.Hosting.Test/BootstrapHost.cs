using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// Builds an in-memory host the way a service does: <c>UseStartup</c> over a
    /// <see cref="Bootstrap"/> subclass, so <c>ConfigureServices</c> and <c>Configure</c> of the
    /// base run in full. The configuration holds an <c>Auth</c> section and nothing else, which
    /// leaves MongoDB, RabbitMQ, Redis and the telemetry exporters switched off.
    /// </summary>
    internal static class BootstrapHost
    {
        /// <summary>The secret the host validates a bearer token's signature with.</summary>
        internal const string SigningSecret = "a-signing-secret-for-the-hosting-tests-0123456789";

        /// <summary>The key the host admits a call between services by.</summary>
        internal const string InternalApiKey = "an-internal-key-for-the-hosting-tests";

        /// <summary>Builds the host of <typeparamref name="TStartup"/> without starting it.</summary>
        internal static IHost Build<TStartup>(CapturedLog? log = null, string environment = "Production", params (string Key, string Value)[] settings)
            where TStartup : Bootstrap =>
            Build<TStartup>(log, environment, services: null, settings);

        /// <summary>
        /// Builds the host of <typeparamref name="TStartup"/> without starting it.
        /// <paramref name="services"/> runs after the startup's own registrations, so what it
        /// registers stands in for what the host cannot construct without a database.
        /// </summary>
        internal static IHost Build<TStartup>(CapturedLog? log, string environment, Action<IServiceCollection>? services, params (string Key, string Value)[] settings)
            where TStartup : Bootstrap
        {
            var configuration = new Dictionary<string, string?>
            {
                ["Auth:Token"] = SigningSecret,
                ["Auth:InternalApiKey"] = InternalApiKey,
            };

            foreach (var (key, value) in settings)
                configuration[key] = value;

            return new HostBuilder()
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .UseEnvironment(environment)
                    .UseStartup<TStartup>()
                    .ConfigureServices(collection =>
                    {
                        if (log is not null)
                            collection.AddLogging(logging => logging.AddProvider(log));

                        services?.Invoke(collection);
                    }))

                // Without MongoDB and Redis the query service and the api key validator cannot
                // be constructed; no test here calls them, so a Development host does not
                // construct every service up front either.
                .UseDefaultServiceProvider(options => options.ValidateOnBuild = false)
                .Build();
        }

        /// <summary>Builds and starts the host of <typeparamref name="TStartup"/>.</summary>
        internal static async Task<IHost> StartAsync<TStartup>(CapturedLog? log = null, string environment = "Production", params (string Key, string Value)[] settings)
            where TStartup : Bootstrap
        {
            var host = Build<TStartup>(log, environment, settings);

            await host.StartAsync();

            return host;
        }

        /// <summary>Builds and starts the host of <typeparamref name="TStartup"/> with registrations of the test's own.</summary>
        internal static async Task<IHost> StartAsync<TStartup>(Action<IServiceCollection> services, string environment = "Production", params (string Key, string Value)[] settings)
            where TStartup : Bootstrap
        {
            var host = Build<TStartup>(log: null, environment, services, settings);

            await host.StartAsync();

            return host;
        }
    }
}

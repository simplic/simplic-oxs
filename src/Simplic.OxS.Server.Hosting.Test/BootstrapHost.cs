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
        /// <summary>Builds the host of <typeparamref name="TStartup"/> without starting it.</summary>
        internal static IHost Build<TStartup>(CapturedLog? log = null, params (string Key, string Value)[] settings)
            where TStartup : Bootstrap
        {
            var configuration = new Dictionary<string, string?>
            {
                ["Auth:Token"] = "a-signing-secret-for-the-hosting-tests-0123456789",
                ["Auth:InternalApiKey"] = "an-internal-key-for-the-hosting-tests",
            };

            foreach (var (key, value) in settings)
                configuration[key] = value;

            return new HostBuilder()
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .UseEnvironment("Production")
                    .UseStartup<TStartup>()
                    .ConfigureServices(services =>
                    {
                        if (log is not null)
                            services.AddLogging(logging => logging.AddProvider(log));
                    }))
                .Build();
        }

        /// <summary>Builds and starts the host of <typeparamref name="TStartup"/>.</summary>
        internal static async Task<IHost> StartAsync<TStartup>(CapturedLog? log = null, params (string Key, string Value)[] settings)
            where TStartup : Bootstrap
        {
            var host = Build<TStartup>(log, settings);

            await host.StartAsync();

            return host;
        }
    }
}

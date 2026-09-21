using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OxQL.Model.Addon;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.Server.Services;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>
    /// Starts a real in-memory host over the package's own controllers: routing, authentication,
    /// authorization, MVC filters and the startup filters all run as they do in a service.
    /// </summary>
    internal static class HardeningTestHost
    {
        /// <summary>The header that authenticates a request against the host's one scheme.</summary>
        internal const string UserHeader = "X-Test-User";

        /// <summary>
        /// Starts a host that serves <paramref name="controllers"/> and nothing else.
        /// </summary>
        /// <param name="controllers">The controllers the host maps; every other controller of the loaded assemblies is left out.</param>
        /// <param name="services">Registrations beyond routing, authentication and MVC.</param>
        /// <param name="environment">The host environment name.</param>
        internal static async Task<IHost> StartAsync(IReadOnlyList<Type> controllers, Action<IServiceCollection> services, string environment = "Production")
        {
            var host = new HostBuilder()
                .ConfigureWebHost(web => web
                    .UseTestServer()
                    .UseEnvironment(environment)
                    .ConfigureServices(collection =>
                    {
                        collection.AddAuthentication(TestAuthentication.Scheme)
                            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.Scheme, null);
                        collection.AddAuthorization();

                        collection.AddControllers().ConfigureApplicationPartManager(manager =>
                        {
                            manager.ApplicationParts.Clear();

                            foreach (var assembly in controllers.Select(controller => controller.Assembly).Distinct())
                                manager.ApplicationParts.Add(new AssemblyPart(assembly));

                            manager.FeatureProviders.Remove(manager.FeatureProviders.OfType<ControllerFeatureProvider>().Single());
                            manager.FeatureProviders.Add(new ListedControllers(controllers));
                        });

                        services(collection);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    }))
                .Build();

            await host.StartAsync();

            return host;
        }

        /// <summary>The registrations the schema controllers need beside the registry itself.</summary>
        internal static void AddSchemaControllerServices(this IServiceCollection services)
        {
            services.AddSingleton<IAddonDefinitionSource>(EmptyAddonDefinitionSource.Instance);
            services.AddScoped<IRequestContext, RequestContext>();
        }

        /// <summary>Fills a schema registration with the fixture host's inputs, outside every fail-fast posture.</summary>
        internal static void Lenient(OxSchemaOptionsBuilder schema)
        {
            var options = SchemaBuild.Options();

            schema.ServiceName = options.ServiceName;
            schema.ApiName = options.ApiName;
            schema.ApiVersion = options.ApiVersion;
            schema.TypeAssemblies = options.TypeAssemblies;
            schema.ControllerTypes = options.ControllerTypes;
            schema.EnvironmentName = "Production";

            // Stated rather than read from the machine, so a build agent's own variables do not decide the posture.
            schema.ContinuousIntegration = false;
        }

        private sealed class ListedControllers(IReadOnlyList<Type> controllers) : ControllerFeatureProvider
        {
            protected override bool IsController(TypeInfo typeInfo) => controllers.Contains(typeInfo.AsType());
        }

        /// <summary>Authenticates a request that names a user in <see cref="UserHeader"/>, and no other.</summary>
        private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            internal const string Scheme = "Test";

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                if (Request.Headers[UserHeader].ToString() is not { Length: > 0 } user)
                    return Task.FromResult(AuthenticateResult.NoResult());

                var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user)], Scheme));

                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
            }
        }
    }
}

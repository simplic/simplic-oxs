using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplic.OxS.Server.Hosting.Test.Fixtures;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// The package's own controllers are served whatever SDK the host project is built with. A
    /// project of the web SDK names the assemblies of its references as application parts; a
    /// project of the plain SDK, as this test project is, does not, so a startup of it that
    /// leaves the parts as the host finds them is such a host.
    /// </summary>
    public sealed class BootstrapApplicationPartTests
    {
        private static int PartsOfThePackage(IServiceProvider services) =>
            services.GetRequiredService<ApplicationPartManager>().ApplicationParts.OfType<AssemblyPart>().Count(part => part.Assembly == typeof(Bootstrap).Assembly);

        [Theory]
        [InlineData("GET", "/schema", HttpStatusCode.OK)]
        [InlineData("GET", "/schema/addons", HttpStatusCode.Unauthorized)]
        [InlineData("GET", "/AddonDefinition/plain.thing", HttpStatusCode.Unauthorized)]
        [InlineData("GET", "/ServiceDefinition", HttpStatusCode.NoContent)]
        [InlineData("POST", "/internal/oxql/batch", HttpStatusCode.Unauthorized)]
        [InlineData("GET", "/OxQL/health?shallow=true", HttpStatusCode.OK)]
        public async Task AHostWhoseProjectNamesNoApplicationParts_ServesTheRoutesOfThePackage(string method, string route, HttpStatusCode status)
        {
            using var host = await BootstrapHost.StartAsync<PlainSdkStartup>(services => services.AddDistributedMemoryCache());
            using var client = host.GetTestClient();
            using var message = new HttpRequestMessage(new HttpMethod(method), "/plainsdk-api/v1" + route);

            if (method == "POST")
                message.Content = new StringContent("""{"queries":[]}""", Encoding.UTF8, "application/json");

            using var answer = await client.SendAsync(message);

            answer.StatusCode.Should().Be(status, await answer.Content.ReadAsStringAsync());
            PartsOfThePackage(host.Services).Should().Be(1);
        }

        [Fact]
        public async Task AHostWhoseProjectNamesThePackageAsAnApplicationPart_HasItOnce()
        {
            using var host = await BootstrapHost.StartAsync<NamedPartStartup>(services => services.AddDistributedMemoryCache());
            using var client = host.GetTestClient();

            PartsOfThePackage(host.Services).Should().Be(1);

            // The routes of the package answer as they do where the host adds the part itself.
            using var answer = await client.GetAsync("/namedpart-api/v1/schema");

            answer.StatusCode.Should().Be(HttpStatusCode.OK, await answer.Content.ReadAsStringAsync());
        }
    }

    /// <summary>A service that leaves the application parts as the host finds them for its project.</summary>
    public class PlainSdkStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Plainsdk";

        protected override Type[] EntityTypes => [];

        protected override void MvcBuilder(IMvcBuilder builder)
        {
        }
    }

    /// <summary>A service whose parts name the package before the host registers its controllers, as a project of the web SDK does.</summary>
    public class NamedPartStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        protected override string ServiceName => "Namedpart";

        protected override Type[] EntityTypes => [];

        protected override void RegisterServices(IServiceCollection services) =>
            services.AddMvcCore().AddApplicationPart(typeof(Bootstrap).Assembly);

        protected override void MvcBuilder(IMvcBuilder builder)
        {
        }
    }
}

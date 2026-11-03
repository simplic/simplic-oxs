using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Simplic.OxS.Server.Hosting.Test.Fixtures;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// What a host answers that is configured with no database (no <c>MongoDB</c> section): the
    /// routes that need none answer, and the others say what is missing.
    /// </summary>
    public sealed class BootstrapWithoutDatabaseTests
    {
        /// <summary>A host with what authenticates a caller and nothing that stands in for a database.</summary>
        private static async Task<IHost> StartAsync(CapturedLog log, string environment)
        {
            var host = BootstrapHost.Build<ParcelStartup>(log, environment, services => services.AddDistributedMemoryCache());

            await host.StartAsync();

            return host;
        }

        private static HttpRequestMessage AsUser(HttpRequestMessage message)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Callers.Bearer());

            return message;
        }

        private static void NothingWentUnhandled(CapturedLog log)
        {
            log.Exceptions.Should().BeEmpty();
            log.Entries.Where(entry => entry.Level >= LogLevel.Error && entry.Message.Contains("exception", StringComparison.OrdinalIgnoreCase))
                .Should().BeEmpty();
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        [InlineData("Development")]
        public async Task TheSchemaDocument_IsServed(string environment)
        {
            var log = new CapturedLog();

            using var host = await StartAsync(log, environment);
            using var client = host.GetTestClient();
            using var answer = await client.GetAsync("/parcel-api/v1/schema");

            var text = await answer.Content.ReadAsStringAsync();

            answer.StatusCode.Should().Be(HttpStatusCode.OK, text);
            JsonNode.Parse(text)!["types"]!.AsObject().ContainsKey("parcel.parcel").Should().BeTrue();
            NothingWentUnhandled(log);
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        [InlineData("Development")]
        public async Task TheAddonDefinitionsOfTheSchema_AreAnsweredNotFoundWithAProblemThatNamesTheMissingSection(string environment)
        {
            var log = new CapturedLog();

            using var host = await StartAsync(log, environment);
            using var client = host.GetTestClient();
            using var answer = await client.SendAsync(AsUser(new HttpRequestMessage(HttpMethod.Get, "/parcel-api/v1/schema/addons")));

            var text = await answer.Content.ReadAsStringAsync();

            answer.StatusCode.Should().Be(HttpStatusCode.NotFound, text);
            answer.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

            var problem = JsonNode.Parse(text)!.AsObject();

            problem["status"]!.GetValue<int>().Should().Be(404);
            problem["title"]!.GetValue<string>().Should().Be("Not Found");
            problem["detail"]!.GetValue<string>().Should().Be("This service holds no addon definitions: it is configured with no database (the MongoDB section).");
            NothingWentUnhandled(log);
        }

        [Fact]
        public async Task TheAddonDefinitionsOfTheSchema_StillNeedACaller()
        {
            using var host = await StartAsync(new CapturedLog(), "Production");
            using var client = host.GetTestClient();
            using var answer = await client.GetAsync("/parcel-api/v1/schema/addons");

            answer.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData("/parcel-api/v1/OxQL/health?shallow=true")]
        [InlineData("/parcel-api/v1/OxQL/health")]
        public async Task TheHealthOfTheQueryEngine_IsAnswered(string path)
        {
            var log = new CapturedLog();

            using var host = await StartAsync(log, "Production");
            using var client = host.GetTestClient();
            using var answer = await client.GetAsync(path);

            var text = await answer.Content.ReadAsStringAsync();

            answer.StatusCode.Should().Be(HttpStatusCode.OK, text);
            JsonNode.Parse(text)!["engine"]!["contract"]!.GetValue<int>().Should().Be(2);
            NothingWentUnhandled(log);
        }

        [Fact]
        public async Task AHostThatValidatesItsServicesWhenItIsBuilt_AsADevelopmentHostDoes_Starts()
        {
            // What a service's Program gets from Host.CreateDefaultBuilder in Development. The
            // cache is configured and never connected to: validation constructs nothing.
            using var host = new HostBuilder()
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Token"] = BootstrapHost.SigningSecret,
                    ["Auth:InternalApiKey"] = BootstrapHost.InternalApiKey,
                    ["Redis:RedisCacheUrl"] = "127.0.0.1:1,abortConnect=false,connectTimeout=300",
                }))
                .ConfigureWebHost(web => web.UseTestServer().UseEnvironment("Development").UseStartup<ParcelStartup>())
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateOnBuild = true;
                    options.ValidateScopes = true;
                })
                .Build();

            await host.StartAsync();

            using var client = host.GetTestClient();
            using var schema = await client.GetAsync("/parcel-api/v1/schema");
            using var health = await client.GetAsync("/parcel-api/v1/OxQL/health?shallow=true");

            schema.StatusCode.Should().Be(HttpStatusCode.OK);
            health.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        private static HostBuilder Validating(string environment, bool cache)
        {
            var configuration = new Dictionary<string, string?>
            {
                ["Auth:Token"] = BootstrapHost.SigningSecret,
                ["Auth:InternalApiKey"] = BootstrapHost.InternalApiKey,
            };

            if (cache)
                configuration["Redis:RedisCacheUrl"] = "127.0.0.1:1,abortConnect=false,connectTimeout=300";

            var builder = new HostBuilder();

            builder.ConfigureAppConfiguration(app => app.AddInMemoryCollection(configuration))
                .ConfigureWebHost(web => web.UseTestServer().UseEnvironment(environment).UseStartup<ParcelStartup>())
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateOnBuild = true;
                    options.ValidateScopes = true;
                });

            return builder;
        }

        [Fact]
        public void AHostWithoutACache_ThatValidatesItsServicesWhenItIsBuilt_DoesNotStart()
        {
            var build = () => Validating("Development", cache: false).Build();

            build.Should().Throw<AggregateException>().Which.Message.Should().Contain("IDistributedCache");
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        [InlineData("Development")]
        public async Task AHostWithoutACache_ThatDoesNotValidate_Starts_AndAnswersOnlyTheRoutesOfControllersThatRequireNoAuthorization(string environment)
        {
            // The hosts of these tests validate nothing when they are built, as a host outside Development does not.
            using var host = await BootstrapHost.StartAsync<ParcelStartup>(new CapturedLog(), environment);
            using var client = host.GetTestClient();

            using var schema = await client.GetAsync("/parcel-api/v1/schema");
            using var open = await client.GetAsync("/parcel-api/v1/Parcel/open");
            using var authorized = await client.SendAsync(AsUser(new HttpRequestMessage(HttpMethod.Get, "/parcel-api/v1/Parcel")));

            schema.StatusCode.Should().Be(HttpStatusCode.OK);

            // A controller that requires authorization authenticates the caller of every action of its, the anonymous ones included, and that needs the cache.
            open.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            authorized.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task AQuery_IsAnsweredInTheEnginesEnvelope_AndTheLogNamesTheMissingSection()
        {
            var log = new CapturedLog();

            using var host = await StartAsync(log, "Production");
            using var client = host.GetTestClient();
            using var message = AsUser(new HttpRequestMessage(HttpMethod.Post, "/parcel-api/v1/OxQL/query")
            {
                Content = new StringContent("""{"entityType":"parcel.parcel","pipeline":[]}""", Encoding.UTF8, "application/json"),
            });

            message.Headers.Add("X-OxQL-Contract", "2");

            using var answer = await client.SendAsync(message);

            var text = await answer.Content.ReadAsStringAsync();

            answer.StatusCode.Should().Be(HttpStatusCode.InternalServerError, text);
            JsonNode.Parse(text)!["type"]!.GetValue<string>().Should().Be("internal_error");
            log.Exceptions.Should().Contain(exception => exception.Message == "This service executes no query: it is configured with no database (the MongoDB section).");
        }
    }
}

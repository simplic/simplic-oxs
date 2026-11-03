using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.Test.OxSchema
{
    /// <summary>
    /// <c>GET /schema</c> is anonymous unless the host requires authorization for it; then it
    /// refuses an unauthenticated caller the way <c>GET /schema/addons</c> does, and serves an
    /// authenticated one the same bytes.
    /// </summary>
    [Collection(SchemaCollection.Name)]
    public sealed class HardeningSchemaAuthorizationTests
    {
        private static readonly Type[] Controllers = [typeof(SchemaController), typeof(ModelDefinitionController)];

        private static Task<IHost> StartAsync(bool requireAuthorization, Action<IServiceCollection>? more = null) =>
            HardeningTestHost.StartAsync(Controllers, services =>
            {
                services.AddSchemaControllerServices();
                services.AddOxSchema(schema =>
                {
                    HardeningTestHost.Lenient(schema);
                    schema.RequireAuthorization = requireAuthorization;
                });

                more?.Invoke(services);
            });

        private static HttpRequestMessage Get(string path, bool authenticated = false, string? ifNoneMatch = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);

            if (authenticated)
                request.Headers.Add(HardeningTestHost.UserHeader, "someone");

            if (ifNoneMatch is not null)
                request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);

            return request;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ByDefault_TheDocumentIsServedToAnyCaller(bool authenticated)
        {
            using var host = await StartAsync(requireAuthorization: false);
            var registry = host.Services.GetRequiredService<OxSchemaRegistry>();

            var response = await host.GetTestClient().SendAsync(Get("/schema", authenticated));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.ETag!.Tag.Should().Be(registry.ETag);
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(registry.Body);
        }

        [Fact]
        public async Task ByDefault_AFallbackPolicyOfTheHostDoesNotReachTheDocument()
        {
            using var host = await StartAsync(requireAuthorization: false, services => services.AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));

            var response = await host.GetTestClient().SendAsync(Get("/schema"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task WhenRequired_AnUnauthenticatedCallerIsRefusedExactlyAsTheAddonRouteRefuses()
        {
            using var host = await StartAsync(requireAuthorization: true);
            var client = host.GetTestClient();

            var schema = await client.SendAsync(Get("/schema"));
            var addons = await client.SendAsync(Get("/schema/addons"));

            addons.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            schema.StatusCode.Should().Be(addons.StatusCode);
            schema.Headers.WwwAuthenticate.ToString().Should().Be(addons.Headers.WwwAuthenticate.ToString());
            (await schema.Content.ReadAsByteArrayAsync()).Should().Equal(await addons.Content.ReadAsByteArrayAsync());
            schema.Headers.ETag.Should().BeNull();
        }

        [Fact]
        public async Task WhenRequired_ARevalidationDoesNotAnswerAnUnauthenticatedCaller()
        {
            using var host = await StartAsync(requireAuthorization: true);
            var tag = host.Services.GetRequiredService<OxSchemaRegistry>().ETag;

            var response = await host.GetTestClient().SendAsync(Get("/schema", ifNoneMatch: tag));

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task WhenRequired_AnAuthenticatedCallerGetsTheSameBytes()
        {
            using var host = await StartAsync(requireAuthorization: true);

            var response = await host.GetTestClient().SendAsync(Get("/schema", authenticated: true));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(SchemaBuild.Build("Production").Body);
        }

        [Fact]
        public async Task WhenRequired_ACallerTheDefaultPolicyRejectsIsForbidden()
        {
            using var host = await StartAsync(requireAuthorization: true, services => services.AddAuthorization(options =>
                options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireClaim("schema-reader").Build()));

            var response = await host.GetTestClient().SendAsync(Get("/schema", authenticated: true));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task WhenRequired_TheModelDefinitionStaysAnonymous()
        {
            using var host = await StartAsync(requireAuthorization: true);

            var response = await host.GetTestClient().SendAsync(Get("/ModelDefinition"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public void TheOption_IsOffUnlessTheHostTurnsItOn()
        {
            new OxSchemaOptionsBuilder().RequireAuthorization.Should().BeFalse();
            SchemaBuild.Degraded.RequireAuthorization.Should().BeFalse();
        }
    }
}

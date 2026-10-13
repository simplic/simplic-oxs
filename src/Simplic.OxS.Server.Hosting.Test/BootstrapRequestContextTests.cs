using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Simplic.OxS.Identity.Extension.Abstraction;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Hosting.Test.Fixtures;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// Who an action finds in the request context, per way a request says who it is made by:
    /// a bearer token, the internal key with the forwarded headers, an api key, and none of them.
    /// </summary>
    public sealed class BootstrapRequestContextTests
    {
        private const string Anonymous = "/who-api/v1/who";
        private const string Internal = "/who-api/v1/internal/who";
        private const string ValidKey = "i-api-key " + BootstrapHost.InternalApiKey;
        private const string WrongKey = "i-api-key another-key";

        /// <summary>Who the bearer tokens of these tests name, who the api key names, and who the forwarded headers name.</summary>
        private static readonly Dictionary<string, Guid?> Ids = new()
        {
            ["-"] = null,
            ["bearer-user"] = Guid.Parse("11111111-0000-4000-8000-000000000001"),
            ["bearer-org"] = Guid.Parse("11111111-0000-4000-8000-000000000002"),
            ["key-user"] = WhoStartup.KeyUser,
            ["key-org"] = WhoStartup.KeyOrganisation,
            ["forwarded-user"] = Guid.Parse("33333333-0000-4000-8000-000000000001"),
            ["forwarded-org"] = Guid.Parse("33333333-0000-4000-8000-000000000002"),
        };

        private static string Token(string? id, string? organisation)
        {
            var claims = new List<Claim>();

            if (id is not null)
                claims.Add(new Claim("Id", id));

            if (organisation is not null)
                claims.Add(new Claim("OId", organisation));

            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(BootstrapHost.SigningSecret));

            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
        }

        /// <summary>A header value of a row: <c>{token}</c> is a bearer token of the bearer's user and organisation, and so on.</summary>
        private static string Value(string written) => written
            .Replace("{token}", Token(Ids["bearer-user"].ToString(), Ids["bearer-org"].ToString()))
            .Replace("{token-without-ids}", Token("not-a-guid", ""))
            .Replace("{token-of-a-user-alone}", Token(Ids["bearer-user"].ToString(), null))
            .Replace("{forwarded-user}", Ids["forwarded-user"].ToString())
            .Replace("{forwarded-org}", Ids["forwarded-org"].ToString());

        public static TheoryData<string, string, string[], int, string, string> Requests => new()
        {
            // what the request presents, the route, its headers, and what the action answers: the status, the user, the organisation
            { "no credentials", Anonymous, [], 200, "-", "-" },

            { "a bearer token", Anonymous, ["Authorization: Bearer {token}"], 200, "bearer-user", "bearer-org" },
            { "a bearer token, the scheme in lower case", Anonymous, ["Authorization: bearer {token}"], 200, "bearer-user", "bearer-org" },
            { "a token under v-bearer, which the host authenticates nobody by", Anonymous, ["Authorization: v-bearer {token}"], 200, "-", "-" },
            { "a token under x-bearer, which the host authenticates nobody by", Anonymous, ["Authorization: x-bearer {token}"], 200, "-", "-" },
            { "a bearer token whose claims are no ids", Anonymous, ["Authorization: Bearer {token-without-ids}"], 200, "-", "-" },
            { "a bearer token of a user without an organisation", Anonymous, ["Authorization: Bearer {token-of-a-user-alone}"], 200, "bearer-user", "-" },
            { "a bearer token that is none", Anonymous, ["Authorization: Bearer not-a-token"], 200, "-", "-" },

            { "the internal key and the forwarded headers", Internal, [$"Authorization: {ValidKey}", "UserId: {forwarded-user}", "OrganizationId: {forwarded-org}"], 200, "forwarded-user", "forwarded-org" },
            { "the internal key and forwarded headers that are no ids", Internal, [$"Authorization: {ValidKey}", "UserId: someone", "OrganizationId: somewhere"], 200, "-", "-" },
            { "the internal key and no forwarded header", Internal, [$"Authorization: {ValidKey}"], 200, "-", "-" },
            { "the internal key and the forwarded user alone", Internal, [$"Authorization: {ValidKey}", "UserId: {forwarded-user}"], 200, "forwarded-user", "-" },
            { "another key and the forwarded headers", Internal, [$"Authorization: {WrongKey}", "UserId: {forwarded-user}", "OrganizationId: {forwarded-org}"], 401, "-", "-" },
            { "a bearer token on a route between services", Internal, ["Authorization: Bearer {token}"], 401, "-", "-" },
            { "the internal scheme and the forwarded headers on a route that checks no key", Anonymous, [$"Authorization: {WrongKey}", "UserId: {forwarded-user}", "OrganizationId: {forwarded-org}"], 200, "forwarded-user", "forwarded-org" },
            { "the forwarded headers alone", Anonymous, ["UserId: {forwarded-user}", "OrganizationId: {forwarded-org}"], 200, "-", "-" },

            { "an api key", Anonymous, [$"x-api-key: {WhoStartup.ApiKey}"], 200, "key-user", "key-org" },
            { "an api key, the header name in another case", Anonymous, [$"X-Api-Key: {WhoStartup.ApiKey}"], 200, "key-user", "key-org" },
            { "an api key the host does not know", Anonymous, ["x-api-key: unknown"], 200, "-", "-" },
            { "an api key beside a bearer token: the token is who the request is made by", Anonymous, [$"x-api-key: {WhoStartup.ApiKey}", "Authorization: Bearer {token}"], 200, "bearer-user", "bearer-org" },
            { "an api key beside the internal key", Internal, [$"x-api-key: {WhoStartup.ApiKey}", $"Authorization: {ValidKey}", "UserId: {forwarded-user}", "OrganizationId: {forwarded-org}"], 200, "-", "-" },

            { "two authorization values", Anonymous, ["Authorization: Bearer {token}", "Authorization: Bearer {token}"], 200, "-", "-" },
            { "a scheme the host does not know", Anonymous, ["Authorization: Basic dXNlcjpwYXNz"], 200, "-", "-" },
        };

        [Theory]
        [MemberData(nameof(Requests))]
        public async Task AnAction_FindsInTheRequestContextWhoTheRequestIsMadeBy(string presented, string route, string[] headers, int status, string user, string organisation)
        {
            using var host = await BootstrapHost.StartAsync<WhoStartup>(WhoStartup.Stores);
            using var client = host.GetTestClient();
            using var message = new HttpRequestMessage(HttpMethod.Get, route);

            foreach (var header in headers)
            {
                var colon = header.IndexOf(':');

                message.Headers.TryAddWithoutValidation(header[..colon], Value(header[(colon + 1)..].Trim()));
            }

            using var answer = await client.SendAsync(message);

            var text = await answer.Content.ReadAsStringAsync();

            ((int)answer.StatusCode).Should().Be(status, $"{presented}: {text}");

            if (answer.StatusCode != HttpStatusCode.OK)
                return;

            var found = JsonNode.Parse(text)!;

            (found["user"]?.GetValue<Guid>(), found["organisation"]?.GetValue<Guid>()).Should().Be((Ids[user], Ids[organisation]), presented);
        }
    }

    /// <summary>What a request context holds, as a route answers it.</summary>
    public sealed record Who(Guid? User, Guid? Organisation);

    /// <summary>A route every caller reaches.</summary>
    [ApiController]
    [AllowAnonymous]
    [Route("who")]
    public class WhoController(IRequestContext context) : ControllerBase
    {
        [HttpGet]
        public Who Get() => new(context.UserId, context.OrganizationId);
    }

    /// <summary>A route between services.</summary>
    [ApiController]
    [Route("internal/who")]
    public class InternalWhoController(IRequestContext context) : OxSInternalController
    {
        [HttpGet]
        public Who Get() => new(context.UserId, context.OrganizationId);
    }

    /// <summary>A service with both routes and one api key it knows.</summary>
    public class WhoStartup(IConfiguration configuration, IWebHostEnvironment environment) : HostingStartup(configuration, environment)
    {
        /// <summary>The api key the host knows.</summary>
        public const string ApiKey = "a-known-api-key";

        /// <summary>Who the api key is.</summary>
        public static readonly Guid KeyUser = Guid.Parse("22222222-0000-4000-8000-000000000001");

        /// <summary>The organisation of the api key.</summary>
        public static readonly Guid KeyOrganisation = Guid.Parse("22222222-0000-4000-8000-000000000002");

        protected override string ServiceName => "Who";

        protected override Type[] EntityTypes => [];

        protected override Type[] Controllers => [typeof(WhoController), typeof(InternalWhoController)];

        /// <summary>What stands in for the stores the host has none of: a cache, and the one api key.</summary>
        public static void Stores(IServiceCollection services)
        {
            services.AddDistributedMemoryCache();
            services.AddTransient<IApiKeyValidator, OneKey>();
        }

        private sealed class OneKey : IApiKeyValidator
        {
            public Task<ApiKeyValidationResult> ValidateKey(string apiKey) =>
                Task.FromResult(apiKey == ApiKey
                    ? new ApiKeyValidationResult { Valid = true, UserId = KeyUser, OrganizationId = KeyOrganisation }
                    : new ApiKeyValidationResult { Valid = false });
        }
    }
}

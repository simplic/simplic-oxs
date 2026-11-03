using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Simplic.OxS.Server.Hosting.Test.Fixtures;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.Services;

namespace Simplic.OxS.Server.Hosting.Test
{
    /// <summary>
    /// Who the query engine takes a request to be made by where it asks before the action runs:
    /// the caller an explain is counted against, and the caller the log line of a contract 1
    /// body names that the host could not bind.
    /// </summary>
    public sealed class BootstrapCallerIdentityTests
    {
        /// <summary>The address every request of these hosts arrives from, as behind one ingress.</summary>
        private const string Ingress = "10.42.0.7";

        private const string Explain = "/parcel-api/v1/OxQL/explain";

        /// <summary>Gives every request one remote address and keeps who each explain was counted against.</summary>
        private sealed class OneAddress(List<string?> counted) : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                app.Use(async (context, pipeline) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(Ingress);

                    await pipeline(context);

                    counted.Add(context.Items.TryGetValue("OxQL.Explain.Identity", out var identity) ? identity as string : null);
                });

                next(app);
            };
        }

        private static string Bearer(Guid user, Guid organisation)
        {
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(BootstrapHost.SigningSecret));
            var token = new JwtSecurityToken(
                claims: [new Claim("Id", user.ToString()), new Claim("OId", organisation.ToString())],
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static HttpRequestMessage Post(string path, string body, string? bearer, int? contract)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

            if (contract is { } number)
                message.Headers.Add("X-OxQL-Contract", number.ToString());

            if (bearer is not null)
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

            return message;
        }

        private static async Task<IHost> StartAsync(List<string?> counted, CapturedLog? log = null)
        {
            var host = BootstrapHost.Build<ParcelStartup>(
                log,
                "Production",
                services =>
                {
                    new AnsweringEngine().Register(services);
                    services.AddSingleton<IStartupFilter>(new OneAddress(counted));
                },
                ("OxQL:Explain:RatePerMinute", "1"),
                ("OxQL:Explain:RateBurst", "1"));

            await host.StartAsync();

            return host;
        }

        [Fact]
        public async Task AnExplain_IsCountedAgainstTheUserOfTheOrganisation_SoCallersBehindOneAddressDoNotShareALimit()
        {
            const string Body = """{"entityType":"parcel.parcel","pipeline":[]}""";

            var (firstUser, firstOrganisation) = (Guid.NewGuid(), Guid.NewGuid());
            var (secondUser, secondOrganisation) = (Guid.NewGuid(), Guid.NewGuid());
            var counted = new List<string?>();

            using var host = await StartAsync(counted);
            using var client = host.GetTestClient();

            // One explain a minute each: the first of either caller is admitted, the second of the first is not.
            using var first = await client.SendAsync(Post(Explain, Body, Bearer(firstUser, firstOrganisation), 2));
            using var second = await client.SendAsync(Post(Explain, Body, Bearer(secondUser, secondOrganisation), 2));
            using var again = await client.SendAsync(Post(Explain, Body, Bearer(firstUser, firstOrganisation), 2));

            first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
            second.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
            again.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            again.Headers.Contains("Retry-After").Should().BeTrue();

            counted.Should().Equal(
                $"{firstOrganisation:N}|{firstUser}",
                $"{secondOrganisation:N}|{secondUser}",
                $"{firstOrganisation:N}|{firstUser}");
        }

        [Fact]
        public async Task AnExplainWithoutCredentials_IsAnsweredBeforeItIsCounted()
        {
            var counted = new List<string?>();

            using var host = await StartAsync(counted);
            using var client = host.GetTestClient();
            using var answer = await client.SendAsync(Post(Explain, """{"entityType":"parcel.parcel","pipeline":[]}""", bearer: null, 2));

            answer.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            counted.Should().Equal([null]);
        }

        [Fact]
        public async Task TheLogLineOfAContract1BodyTheHostCouldNotBind_NamesTheCallerAndTheCorrelationIdOfTheRequest()
        {
            var (user, organisation, correlation) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var log = new CapturedLog();

            using var host = await StartAsync([], log);
            using var client = host.GetTestClient();
            using var message = Post("/parcel-api/v1/OxQL/query", """{"entityType":"parcel.parcel","pipeline":{}}""", Bearer(user, organisation), contract: null);

            message.Headers.Add("X-Correlation-ID", correlation.ToString());

            using var answer = await client.SendAsync(message);

            answer.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            log.Entries.Should().ContainSingle(entry => entry.Message.Contains("reason=unbound", StringComparison.Ordinal))
                .Which.Message.Should().Contain($"user={user} org={organisation} correlation={correlation}");
        }

        [Fact]
        public async Task TheLogLineOfAContract1BodyTheInternalRouteCouldNotBind_NamesWhoTheCallIsMadeFor_OnceTheKeyWasChecked()
        {
            var (user, organisation) = (Guid.NewGuid(), Guid.NewGuid());
            var log = new CapturedLog();

            using var host = await StartAsync([], log);
            using var client = host.GetTestClient();

            foreach (var key in new[] { BootstrapHost.InternalApiKey, "another-key" })
            {
                using var message = Post("/parcel-api/v1/internal/oxql/batch", """{"queries":{}}""", bearer: null, contract: null);

                message.Headers.TryAddWithoutValidation("Authorization", $"i-api-key {key}");
                message.Headers.Add("UserId", user.ToString());
                message.Headers.Add("OrganizationId", organisation.ToString());

                using var answer = await client.SendAsync(message);

                answer.StatusCode.Should().Be(key == BootstrapHost.InternalApiKey ? HttpStatusCode.BadRequest : HttpStatusCode.Unauthorized);
            }

            // One line: of the call that presented the key. The other was answered before anything read its headers.
            log.Entries.Should().ContainSingle(entry => entry.Message.Contains("reason=unbound", StringComparison.Ordinal))
                .Which.Message.Should().Contain($"user={user} org={organisation}");
        }

        private static DefaultHttpContext Request(string? authorization, ClaimsPrincipal? principal = null, params (string Name, string Value)[] headers)
        {
            var context = new DefaultHttpContext();

            if (authorization is not null)
                context.Request.Headers.Authorization = authorization;

            foreach (var (name, value) in headers)
                context.Request.Headers[name] = value;

            if (principal is not null)
                context.User = principal;

            return context;
        }

        private static ClaimsPrincipal Principal(Guid user, Guid organisation) =>
            new(new ClaimsIdentity([new Claim("Id", user.ToString()), new Claim("OId", organisation.ToString())], "test"));

        private static async Task<(string? User, Guid? Organisation)> AskAsync(OxQLScopeProvider provider, HttpContext? context) =>
            (provider.UserId(context), await provider.OrganisationAsync(context, CancellationToken.None));

        [Theory]
        [InlineData("Bearer token", null)]
        [InlineData("bearer token", null)]
        [InlineData("v-bearer token", null)]
        [InlineData("x-bearer token", null)]
        [InlineData(null, "an-api-key")]
        public async Task BeforeTheContextIsFilled_TheProviderNamesTheAuthenticatedPrincipal(string? authorization, string? apiKey)
        {
            var (user, organisation) = (Guid.NewGuid(), Guid.NewGuid());
            var context = apiKey is null
                ? Request(authorization, Principal(user, organisation))
                : Request(authorization, Principal(user, organisation), ("x-api-key", apiKey));

            (await AskAsync(new OxQLScopeProvider(new RequestContext()), context)).Should().Be((user.ToString(), organisation));
        }

        [Fact]
        public async Task BeforeTheContextIsFilled_ARequestThatAuthenticatedNobody_HasNeitherUserNorOrganisation()
        {
            var provider = new OxQLScopeProvider(new RequestContext());
            var forwarded = new[] { ("UserId", Guid.NewGuid().ToString()), ("OrganizationId", Guid.NewGuid().ToString()) };

            // No credentials; a bearer token that authenticated nobody; headers alone; and the
            // internal scheme whose key nothing has checked yet.
            (await AskAsync(provider, Request(null))).Should().Be((null, null));
            (await AskAsync(provider, Request("Bearer not-a-token"))).Should().Be((null, null));
            (await AskAsync(provider, Request(null, null, forwarded))).Should().Be((null, null));
            (await AskAsync(provider, Request("i-api-key some-key", null, forwarded))).Should().Be((null, null));
            (await AskAsync(provider, Request("i-api-key some-key", Principal(Guid.NewGuid(), Guid.NewGuid()), forwarded))).Should().Be((null, null));
            (await AskAsync(provider, null)).Should().Be((null, null));
        }

        [Fact]
        public async Task BeforeTheContextIsFilled_TheForwardedHeadersOfAnInternalCallCount_OnceItsKeyWasChecked()
        {
            var (user, organisation) = (Guid.NewGuid(), Guid.NewGuid());
            var context = Request("i-api-key the-key", null, ("UserId", user.ToString()), ("OrganizationId", organisation.ToString()));

            // What the internal key's filter leaves on a request it admitted.
            context.Items["Simplic.OxS.InternalKeyAdmitted"] = true;

            (await AskAsync(new OxQLScopeProvider(new RequestContext()), context)).Should().Be((user.ToString(), organisation));
        }

        [Fact]
        public async Task TheFilledContext_IsWhatTheProviderAnswers_WhateverTheRequestSays()
        {
            var (user, organisation, correlation) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var provider = new OxQLScopeProvider(new RequestContext { UserId = user, OrganizationId = organisation, CorrelationId = correlation });
            var context = Request("Bearer token", Principal(Guid.NewGuid(), Guid.NewGuid()), ("X-Correlation-ID", Guid.NewGuid().ToString()));

            (await AskAsync(provider, context)).Should().Be((user.ToString(), organisation));
            provider.CorrelationId(context).Should().Be(correlation.ToString());

            // A context that holds an organisation and no user is filled too.
            var withoutUser = new OxQLScopeProvider(new RequestContext { OrganizationId = organisation });

            (await AskAsync(withoutUser, context)).Should().Be((null, organisation));
        }

        [Fact]
        public void BeforeTheContextIsFilled_TheCorrelationIdIsTheRequestHeaders_AndTheTraceIdentifierWhereItIsNoId()
        {
            var correlation = Guid.NewGuid();
            var provider = new OxQLScopeProvider(new RequestContext());

            provider.CorrelationId(Request(null, null, ("X-Correlation-ID", correlation.ToString()))).Should().Be(correlation.ToString());

            var without = Request(null, null, ("X-Correlation-ID", "not-an-id"));

            without.TraceIdentifier = "trace-7";
            provider.CorrelationId(without).Should().Be("trace-7");
            provider.CorrelationId(null).Should().BeNull();
        }
    }
}

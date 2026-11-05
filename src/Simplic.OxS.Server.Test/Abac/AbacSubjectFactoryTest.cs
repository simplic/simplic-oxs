using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Simplic.OxS.ABAC.Subject;
using Simplic.OxS.ABAC.Sync;
using Simplic.OxS.Server.Abac;
using Simplic.OxS.Settings;
using Xunit;
using static Simplic.OxS.Server.Test.Abac.AbacTestData;

namespace Simplic.OxS.Server.Test.Abac;

/// <summary>Tests for the server-side subject resolution (factory, cached provider, Identity HTTP client).</summary>
public class AbacSubjectFactoryTest
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid TeamId = Guid.NewGuid();

    private static AbacSubjectAttributes Attributes(params string[] roles) => new()
    {
        Roles = [.. roles],
        Teams = [new AbacTeam { Id = TeamId, Name = "hr" }],
        Email = "lookup@example.com"
    };

    private static IHttpContextAccessor Http(params Claim[] claims) => new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
    };

    private static Claim[] IdentityClaims(params Claim[] extra) => [new("Id", UserId.ToString()), new("OId", OrgId.ToString()), .. extra];

    private static AbacSubjectFactory Factory(IHttpContextAccessor http, IAbacSubjectAttributeProvider provider)
        => new(http, provider, NullLogger<AbacSubjectFactory>.Instance);

    // ── Factory ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Factory_PopulatesRolesAndTeamsFromLookup_IgnoringTokenRoles()
    {
        var provider = new Mock<IAbacSubjectAttributeProvider>();
        provider.Setup(p => p.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(Attributes("admin", "viewer"));

        var subject = await Factory(Http(IdentityClaims(new Claim("Roles", """["owner"]"""))), provider.Object).BuildSubjectAsync();

        subject.Should().NotBeNull();
        subject!.OrganizationId.Should().Be(OrgId);
        subject.User.Id.Should().Be(UserId);
        subject.User.Email.Should().Be("lookup@example.com");
        subject.Roles.Should().Equal("admin", "viewer");
        subject.Teams.Should().ContainSingle(t => t.Id == TeamId && t.Name == "hr");
    }

    [Fact]
    public async Task Factory_PrefersEmailClaim()
    {
        var provider = new Mock<IAbacSubjectAttributeProvider>();
        provider.Setup(p => p.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(Attributes("viewer"));

        var subject = await Factory(Http(IdentityClaims(new Claim(ClaimTypes.Email, "claim@example.com"))), provider.Object).BuildSubjectAsync();

        subject!.User.Email.Should().Be("claim@example.com");
    }

    [Fact]
    public async Task Factory_ReturnsNull_WhenLookupFails()
    {
        var provider = new Mock<IAbacSubjectAttributeProvider>();
        provider.Setup(p => p.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("identity down"));

        (await Factory(Http(IdentityClaims()), provider.Object).BuildSubjectAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Factory_ReturnsNull_WhenUserIsNotAMember()
    {
        var provider = new Mock<IAbacSubjectAttributeProvider>();
        provider.Setup(p => p.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>())).ReturnsAsync((AbacSubjectAttributes?)null);

        (await Factory(Http(IdentityClaims()), provider.Object).BuildSubjectAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Factory_ReturnsNull_WithoutLookup_WhenOrganizationClaimIsMissing()
    {
        var provider = new Mock<IAbacSubjectAttributeProvider>(MockBehavior.Strict);

        (await Factory(Http(new Claim("Id", UserId.ToString())), provider.Object).BuildSubjectAsync()).Should().BeNull();
    }

    // ── Cached provider ───────────────────────────────────────────────────────

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static AbacSubjectAttributeProvider Provider(IAbacSubjectAttributeClient client, Microsoft.Extensions.Caching.Distributed.IDistributedCache? redis = null, TimeProvider? time = null)
        => new(client, NewMemoryCache(), NullLogger<AbacSubjectAttributeProvider>.Instance, Options(), redis, time);

    [Fact]
    public async Task Provider_CacheHit_AvoidsSecondLookup()
    {
        var client = new Mock<IAbacSubjectAttributeClient>();
        client.Setup(c => c.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(Attributes("admin"));
        var provider = Provider(client.Object);

        (await provider.GetAttributesAsync(UserId, OrgId))!.Roles.Should().Equal("admin");
        (await provider.GetAttributesAsync(UserId, OrgId))!.Roles.Should().Equal("admin");

        client.Verify(c => c.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Provider_SharedRedisEntry_AvoidsLookupOnOtherInstance()
    {
        var redis = NewDistributedCache();
        var client = new Mock<IAbacSubjectAttributeClient>();
        client.Setup(c => c.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>())).ReturnsAsync(Attributes("admin"));

        await Provider(client.Object, redis).GetAttributesAsync(UserId, OrgId);
        var other = await Provider(client.Object, redis).GetAttributesAsync(UserId, OrgId);

        other!.Teams.Should().ContainSingle(t => t.Id == TeamId);
        client.Verify(c => c.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Provider_RefetchesAfterTtl()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var client = new Mock<IAbacSubjectAttributeClient>();
        client.SetupSequence(c => c.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Attributes("admin"))
            .ReturnsAsync(Attributes("viewer"));
        var provider = Provider(client.Object, NewDistributedCache(), time);

        await provider.GetAttributesAsync(UserId, OrgId);
        time.Now += new AbacSubjectOptions().Ttl + TimeSpan.FromSeconds(1);

        (await provider.GetAttributesAsync(UserId, OrgId))!.Roles.Should().Equal("viewer");
    }

    [Fact]
    public async Task Provider_DoesNotCacheFailures()
    {
        var client = new Mock<IAbacSubjectAttributeClient>();
        client.SetupSequence(c => c.GetAttributesAsync(UserId, OrgId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("identity down"))
            .ReturnsAsync(Attributes("admin"));
        var provider = Provider(client.Object);

        await provider.Invoking(p => p.GetAttributesAsync(UserId, OrgId)).Should().ThrowAsync<HttpRequestException>();
        (await provider.GetAttributesAsync(UserId, OrgId))!.Roles.Should().Equal("admin");
    }

    // ── Identity HTTP client ──────────────────────────────────────────────────

    private sealed class Handler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static AbacSubjectAttributeHttpClient Client(HttpMessageHandler handler, string? identityHost = "simplic_identity")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(identityHost == null ? [] : [new("InternalHosts:identity", identityHost)])
            .Build();
        var auth = Microsoft.Extensions.Options.Options.Create(new AuthSettings { InternalApiKey = "key" });
        return new AbacSubjectAttributeHttpClient(new HttpClient(handler), auth, Options(), configuration);
    }

    [Fact]
    public async Task Client_CallsInternalEndpoint_AndParsesAttributes()
    {
        var handler = new Handler(HttpStatusCode.OK, $$"""{"roles":["admin"],"teams":[{"id":"{{TeamId}}","name":"hr"}],"email":"a@b.c"}""");

        var attributes = await Client(handler).GetAttributesAsync(UserId, OrgId);

        handler.Request!.RequestUri!.ToString().Should().Be(
            $"http://simplic_identity/identity-api/v1/internal/AbacSubject/attributes?userId={UserId}&organizationId={OrgId}");
        handler.Request.Headers.Authorization!.Parameter.Should().Be("key");
        handler.Request.Headers.GetValues(Constants.HttpHeaderOrganizationIdKey).Should().Equal(OrgId.ToString());
        attributes!.Roles.Should().Equal("admin");
        attributes.Teams.Should().ContainSingle(t => t.Id == TeamId && t.Name == "hr");
    }

    [Fact]
    public async Task Client_ReturnsNull_OnNotFound_AndThrows_OnServerError()
    {
        (await Client(new Handler(HttpStatusCode.NotFound)).GetAttributesAsync(UserId, OrgId)).Should().BeNull();

        await Client(new Handler(HttpStatusCode.ServiceUnavailable))
            .Invoking(c => c.GetAttributesAsync(UserId, OrgId)).Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public void Client_Throws_WhenIdentityHostIsNotConfigured()
    {
        Client(new Handler(HttpStatusCode.OK), identityHost: null)
            .Invoking(c => c.BuildUrl(UserId, OrgId)).Should().Throw<InvalidOperationException>();
    }
}

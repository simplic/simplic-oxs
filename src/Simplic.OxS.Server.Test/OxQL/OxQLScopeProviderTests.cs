using Microsoft.AspNetCore.Http;
using OxQL.AspNetCore.Scope;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.Services;

namespace Simplic.OxS.Server.Test.OxQL
{
    /// <summary>The organisation scope the engine applies at every entry into an entity comes from the request context.</summary>
    public sealed class OxQLScopeProviderTests
    {
        [Fact]
        public async Task Organisation_IsTheRequestContexts()
        {
            var organisation = Guid.NewGuid();
            IOxQLScopeProvider provider = new OxQLScopeProvider(new RequestContext { OrganizationId = organisation });

            (await provider.OrganisationAsync(new DefaultHttpContext(), CancellationToken.None)).Should().Be(organisation);
        }

        [Fact]
        public async Task Organisation_IsNullWhenTheContextCarriesNone()
        {
            // The engine answers 403 on null; it never compares against null.
            IOxQLScopeProvider provider = new OxQLScopeProvider(new RequestContext());

            (await provider.OrganisationAsync(null, CancellationToken.None)).Should().BeNull();
        }

        [Fact]
        public void UserAndCorrelation_AreTheRequestContexts()
        {
            var user = Guid.NewGuid();
            var correlation = Guid.NewGuid();
            IOxQLScopeProvider provider = new OxQLScopeProvider(new RequestContext { UserId = user, CorrelationId = correlation });

            provider.UserId(null).Should().Be(user.ToString());
            provider.CorrelationId(null).Should().Be(correlation.ToString());
        }

        [Fact]
        public void Correlation_FallsBackToTheTraceIdentifier()
        {
            IOxQLScopeProvider provider = new OxQLScopeProvider(new RequestContext());
            var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-1" };

            provider.UserId(httpContext).Should().BeNull();
            provider.CorrelationId(httpContext).Should().Be("trace-1");
        }
    }
}

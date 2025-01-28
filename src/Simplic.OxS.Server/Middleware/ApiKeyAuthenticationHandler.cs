using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.Server.Interface;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Simplic.OxS.Server.Middleware;

public class ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyValidator apiKeyValidator) :
    AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey(Constants.HttpHeaderApiKey))
            return AuthenticateResult.NoResult();

        var apiKey = Request.Headers[Constants.HttpHeaderApiKey].ToString();

        if (!(await apiKeyValidator.TryValidateApiKeyAsync(apiKey, out var userId, out var organizationId)))
            return AuthenticateResult.Fail("Invalid API Key");

        var claims = new List<Claim>
        {
            new Claim("Id", userId?.ToString() ?? ""),
            new Claim("OId", organizationId?.ToString() ?? "")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

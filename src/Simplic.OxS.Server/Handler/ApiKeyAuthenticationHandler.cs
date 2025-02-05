using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.Identity.Extension.Abstraction;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Simplic.OxS.Server.Handler;

/// <summary>
/// Authentication handler to authenticate with api keys.
/// </summary>
public class ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyValidator apiKeyValidator) :
    AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    /// Handles the authentication.
    /// </summary>
    /// <returns></returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Constants.HttpHeaderApiKey, out var value))
            return AuthenticateResult.NoResult();

        if (Request.Headers.Authorization.Count != 0)
            return AuthenticateResult.Fail("Api keys are only allwed witout other authentication");

        var apiKey = value.ToString();

        var result = await apiKeyValidator.ValidateKey(apiKey);

        if (!result.Valid)
            return AuthenticateResult.Fail("Invalid API Key");

        var claims = new List<Claim>
        {
            new Claim("Id", result?.UserId?.ToString() ?? ""),
            new Claim("OId", result?.OrganizationId?.ToString() ?? "")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

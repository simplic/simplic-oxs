using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Simplic.OxS.ABAC.Sync;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Abac.Sync;

/// <summary>
/// Pulls an organization's active rule set from Identity (§12.6). Abstracted for testability.
/// </summary>
public interface IAbacRuleSetClient
{
    /// <summary>
    /// Returns the active rule set of <paramref name="organizationId"/>, or <c>null</c> when Identity answers 404.
    /// Throws on transport errors or any other non-success status.
    /// </summary>
    Task<AbacOrganizationRuleSet?> GetRuleSetAsync(Guid organizationId, CancellationToken ct = default);
}

/// <summary>
/// Typed <see cref="HttpClient"/> implementation of <see cref="IAbacRuleSetClient"/>.
/// <para>
/// Calls <c>GET {Scheme}://{InternalHosts[identity]}/identity-api/{ApiVersion}/internal/AbacPolicySync/rule-set</c>
/// (same URL scheme as <c>InternalClientBase.BuildUrl</c>; override via <c>Abac:Sync:BaseUrl</c>) with the
/// internal-key authorization (<c>Authorization: i-api-key {Auth:InternalApiKey}</c>) and the
/// <c>OrganizationId</c> header carrying the organization.
/// </para>
/// </summary>
public class AbacRuleSetHttpClient : IAbacRuleSetClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<AuthSettings> _authSettings;
    private readonly IOptions<AbacRuntimeOptions> _options;
    private readonly IConfiguration _configuration;

    /// <summary>Initializes a new instance of <see cref="AbacRuleSetHttpClient"/>.</summary>
    public AbacRuleSetHttpClient(HttpClient httpClient, IOptions<AuthSettings> authSettings, IOptions<AbacRuntimeOptions> options, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _authSettings = authSettings;
        _options = options;
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public async Task<AbacOrganizationRuleSet?> GetRuleSetAsync(Guid organizationId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl());
        request.Headers.Authorization = new AuthenticationHeaderValue(Constants.HttpAuthorizationSchemeInternalKey, _authSettings.Value.InternalApiKey);
        request.Headers.Add(Constants.HttpHeaderOrganizationIdKey, organizationId.ToString());

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"ABAC rule-set pull failed with status {(int)response.StatusCode}.", null, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(json) ? null : AbacRuleSetJson.Deserialize<AbacOrganizationRuleSet>(json);
    }

    /// <summary>Builds the pull URL.</summary>
    public string BuildUrl()
    {
        var sync = _options.Value.Sync;
        if (!string.IsNullOrWhiteSpace(sync.BaseUrl))
            return sync.BaseUrl;

        var host = _configuration[$"InternalHosts:{sync.Service}"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException($"ABAC: InternalHosts:{sync.Service} is not configured — cannot pull rule sets.");

        return $"{sync.Scheme}://{host}/{sync.Service}-api/{sync.ApiVersion}/internal/{AbacSyncEndpoints.Controller}/{AbacSyncEndpoints.RuleSet}";
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Simplic.OxS.ABAC.Sync;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// Fetches the live subject attributes (roles/teams) from Identity. Abstracted for testability.
/// </summary>
public interface IAbacSubjectAttributeClient
{
    /// <summary>
    /// Returns the attributes of <paramref name="userId"/> within <paramref name="organizationId"/>, or <c>null</c> when
    /// Identity answers 404 (unknown user / not a member). Throws on transport errors or any other non-success status.
    /// </summary>
    Task<AbacSubjectAttributes?> GetAttributesAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
}

/// <summary>
/// Typed <see cref="HttpClient"/> implementation of <see cref="IAbacSubjectAttributeClient"/>, same pattern as
/// <see cref="Sync.AbacRuleSetHttpClient"/>: calls
/// <c>GET {Scheme}://{InternalHosts[identity]}/identity-api/{ApiVersion}/internal/AbacSubject/attributes?userId=..&amp;organizationId=..</c>
/// (override via <c>Abac:Subject:BaseUrl</c>) with <c>Authorization: i-api-key {Auth:InternalApiKey}</c> and the
/// <c>OrganizationId</c>/<c>UserId</c> headers.
/// </summary>
public class AbacSubjectAttributeHttpClient : IAbacSubjectAttributeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IOptions<AuthSettings> _authSettings;
    private readonly IOptions<AbacRuntimeOptions> _options;
    private readonly IConfiguration _configuration;

    /// <summary>Initializes a new instance of <see cref="AbacSubjectAttributeHttpClient"/>.</summary>
    public AbacSubjectAttributeHttpClient(HttpClient httpClient, IOptions<AuthSettings> authSettings, IOptions<AbacRuntimeOptions> options, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _authSettings = authSettings;
        _options = options;
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public async Task<AbacSubjectAttributes?> GetAttributesAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(userId, organizationId));
        request.Headers.Authorization = new AuthenticationHeaderValue(Constants.HttpAuthorizationSchemeInternalKey, _authSettings.Value.InternalApiKey);
        request.Headers.Add(Constants.HttpHeaderOrganizationIdKey, organizationId.ToString());
        request.Headers.Add(Constants.HttpHeaderUserIdKey, userId.ToString());

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"ABAC subject attribute lookup failed with status {(int)response.StatusCode}.", null, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            throw new HttpRequestException("ABAC subject attribute lookup returned an empty body.");

        return JsonSerializer.Deserialize<AbacSubjectAttributes>(json, JsonOptions)
            ?? throw new HttpRequestException("ABAC subject attribute lookup returned an invalid body.");
    }

    /// <summary>Builds the lookup URL including the query string.</summary>
    public string BuildUrl(Guid userId, Guid organizationId)
    {
        var query = $"{AbacSubjectEndpoints.UserIdParameter}={userId}&{AbacSubjectEndpoints.OrganizationIdParameter}={organizationId}";

        var baseUrl = _options.Value.Subject.BaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl))
            return $"{baseUrl}{(baseUrl.Contains('?') ? '&' : '?')}{query}";

        var sync = _options.Value.Sync;
        var host = _configuration[$"InternalHosts:{sync.Service}"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException($"ABAC: InternalHosts:{sync.Service} is not configured — cannot resolve subject attributes.");

        return $"{sync.Scheme}://{host}/{sync.Service}-api/{sync.ApiVersion}/internal/{AbacSubjectEndpoints.Controller}/{AbacSubjectEndpoints.Attributes}?{query}";
    }
}

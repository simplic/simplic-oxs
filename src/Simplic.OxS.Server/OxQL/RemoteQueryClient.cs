using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OxQL.AspNetCore;
using OxQL.AspNetCore.Scope;
using OxQL.Core.Engine;
using OxQL.Core.Models;
using Simplic.OxS.Settings;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// Sends the query engine's remote resolve and semi-join batches to the service that owns the
/// target entity: <c>POST http://{InternalHosts[ns]}/{ns}-api/{InternalApiVersions[ns]}/internal/oxql/batch</c>,
/// where <c>ns</c> is the target entity's namespace, which is the owner's service name, which
/// is the owner's api name. The internal key, the caller's user, organisation and correlation
/// and the contract header travel on every call; the owner binds and scopes the batch as its
/// own. A per-request message over a named <see cref="IHttpClientFactory"/> client, cancelled
/// by the engine's token and its budget; never the shared-header internal client.
/// </summary>
public sealed class RemoteQueryClient : IRemoteQueryClient
{
    /// <summary>The named client every call goes through.</summary>
    public const string HttpClientName = "OxQL.Remote";

    /// <summary>The configuration section naming the owner's api version per service; a service without an entry is <c>v1</c>.</summary>
    public const string ApiVersionsSection = "InternalApiVersions";

    /// <summary>The configuration section naming the host per service, shared with the internal client.</summary>
    public const string HostsSection = "InternalHosts";

    private const string DefaultApiVersion = "v1";
    private static readonly TimeSpan HealthBudget = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The bound of a batch whose caller names no positive budget: the query engine's default
    /// ceiling for one request. No outbound call waits on the HTTP client's own timeout.
    /// </summary>
    internal TimeSpan FallbackBudget { get; set; } = TimeSpan.FromSeconds(10);

    private readonly IHttpClientFactory clients;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<RemoteQueryClient> logger;
    private readonly string internalApiKey;
    private readonly IReadOnlyDictionary<string, string> hosts;
    private readonly IReadOnlyDictionary<string, string> versions;

    /// <summary>
    /// Creates the client. The hosts, the api versions and the internal key are read once,
    /// here; a change to the configuration needs a restart, as it does for the internal client.
    /// </summary>
    /// <param name="clients">The factory of the named client every call goes through.</param>
    /// <param name="auth">The settings holding the internal api key.</param>
    /// <param name="configuration">The host's configuration, for <see cref="HostsSection"/> and <see cref="ApiVersionsSection"/>.</param>
    /// <param name="httpContextAccessor">The current request, whose scope provider names the identity to forward.</param>
    /// <param name="logger">The logger.</param>
    public RemoteQueryClient(
        IHttpClientFactory clients,
        IOptions<AuthSettings> auth,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RemoteQueryClient> logger)
    {
        this.clients = clients ?? throw new ArgumentNullException(nameof(clients));
        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(configuration);

        internalApiKey = auth.Value.InternalApiKey;
        hosts = Map(configuration, HostsSection);
        versions = Map(configuration, ApiVersionsSection);
    }

    /// <summary>The owner's batch route for a service key, or null when the host does not know the service.</summary>
    public string? BatchUrl(string serviceKey) => Url(serviceKey, "internal/oxql/batch");

    /// <summary>
    /// The owner's health route for a service key, or null when the host does not know the
    /// service. The shallow form: the owner answers without its own remote state and starts no
    /// measurement of its own, so a probe never sets off the probed service's probes.
    /// </summary>
    public string? HealthUrl(string serviceKey) => Url(serviceKey, "OxQL/health?shallow=true");

    /// <inheritdoc/>
    public bool IsConfigured(string serviceKey) => serviceKey is not null && hosts.ContainsKey(serviceKey);

    /// <inheritdoc/>
    public async Task<BatchResponse> BatchAsync(string serviceKey, BatchRequest request, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var url = BatchUrl(serviceKey) ?? throw new InvalidOperationException($"No '{HostsSection}' entry for '{serviceKey}'; the service is not configured on this host.");

        // A host entry that does not form an address is an owner that cannot be reached, and
        // surfaces as the transport failure every other unreachable owner surfaces as.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
        {
            logger.LogWarning("OxQL remote batch to {Service} was not sent: its '{Section}' entry does not form a valid address", serviceKey, HostsSection);

            throw new HttpRequestException($"The '{HostsSection}' entry for '{serviceKey}' does not form a valid address.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, address)
        {
            Content = JsonContent.Create(request, options: OxQLJson.Wire),
        };

        await ForwardAsync(message, cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(budget > TimeSpan.Zero ? budget : FallbackBudget);

        using var response = await clients.CreateClient(HttpClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("OxQL remote batch to {Service} answered {Status}", serviceKey, (int)response.StatusCode);

            throw new HttpRequestException($"The owner of '{serviceKey}' answered {(int)response.StatusCode} to the internal batch.", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<BatchResponse>(OxQLJson.Wire, timeout.Token)
            ?? throw new HttpRequestException($"The owner of '{serviceKey}' answered the internal batch with an empty body.");
    }

    /// <inheritdoc/>
    public async Task<bool> IsReachableAsync(string serviceKey, CancellationToken cancellationToken)
    {
        if (HealthUrl(serviceKey) is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out var address))
            return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(HealthBudget);

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, address);
            using var response = await clients.CreateClient(HttpClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            return response.IsSuccessStatusCode;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private string? Url(string serviceKey, string path)
    {
        if (serviceKey is null || !hosts.TryGetValue(serviceKey, out var host))
            return null;

        var version = versions.TryGetValue(serviceKey, out var configured) && !string.IsNullOrWhiteSpace(configured) ? configured : DefaultApiVersion;

        return $"http://{host}/{serviceKey}-api/{version}/{path}";
    }

    /// <summary>
    /// The internal key, the caller's identity and the contract header.
    /// <para>
    /// The identity is read from the <see cref="IOxQLScopeProvider"/> the engine scoped the
    /// parent query with, not from the request context directly: the owner then answers under
    /// the same organisation the parent rows were selected under, by construction rather than
    /// by both sides happening to read the same thing. The key authorises the call; the
    /// forwarded user is who it is made for, and the owner scopes on that.
    /// </para>
    /// </summary>
    private async Task ForwardAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue(Constants.HttpAuthorizationSchemeInternalKey, internalApiKey);
        message.Headers.TryAddWithoutValidation(OxQLQueryService.ContractHeader, EngineCapabilities.Contract.ToString());

        var httpContext = httpContextAccessor.HttpContext;
        var scope = httpContext?.RequestServices.GetService<IOxQLScopeProvider>();

        if (scope is null)
            return;

        if (await scope.OrganisationAsync(httpContext, cancellationToken) is { } organisation)
            message.Headers.TryAddWithoutValidation(Constants.HttpHeaderOrganizationIdKey, organisation.ToString());

        if (scope.UserId(httpContext) is { Length: > 0 } user)
            message.Headers.TryAddWithoutValidation(Constants.HttpHeaderUserIdKey, user);

        if (scope.CorrelationId(httpContext) is { Length: > 0 } correlation)
            message.Headers.TryAddWithoutValidation(Constants.HttpHeaderCorrelationIdKey, correlation);
    }

    private static IReadOnlyDictionary<string, string> Map(IConfiguration configuration, string section)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var child in configuration.GetSection(section).GetChildren())
            if (!string.IsNullOrWhiteSpace(child.Value))
                map[child.Key] = child.Value.Trim();

        return map;
    }
}

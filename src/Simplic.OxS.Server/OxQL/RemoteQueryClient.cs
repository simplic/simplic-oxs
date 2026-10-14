using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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
/// is the owner's api name. The explain of a query's continued parts goes to the owner's
/// <c>internal/oxql/explain</c> beside it, every check of one round of an explain in one call. The internal key, the caller's user, organisation and
/// correlation and the contract header travel on every call, and no other header; the owner
/// binds and scopes the call as its own. A per-request message over a named
/// <see cref="IHttpClientFactory"/> client, cancelled by the engine's token and its budget;
/// never the shared-header internal client.
/// <para>
/// What the owner's shallow health says of it (engine version, contract, batch cap, page cap) is
/// read where reachability is measured, and when the engine asks for an owner whose facts are
/// unknown or older than the health probe's time to live before a request's first batch
/// (<see cref="OwnerOfAsync"/>), and kept per service (<see cref="IRemoteOwnerInfo"/>); facts
/// older than that are unknown again. The engine sizes its batches by them.
/// </para>
/// <para>
/// An <c>InternalHosts</c> value is a host with an optional port and nothing else
/// (<see cref="HostEntryForm"/>). The client calls no address formed from any other value: the
/// service then counts as not configured (<see cref="IsConfigured"/>), and
/// <see cref="RefusedHostOf"/> says what the value has that a host has not. The named client
/// follows no redirect: every call goes to the configured host and nowhere else, and an owner
/// that answers one with a redirect does not serve the route there.
/// </para>
/// </summary>
public sealed partial class RemoteQueryClient : IRemoteQueryClient, IRemoteOwnerInfo
{
    /// <summary>The named client every call goes through.</summary>
    public const string HttpClientName = "OxQL.Remote";

    /// <summary>The header naming the service that makes an internal call: what the owner's internal explain counts its places in flight by.</summary>
    public const string CallerHeader = "X-OxQL-Caller";

    /// <summary>The name of this service, sent as <see cref="CallerHeader"/>; no header without it.</summary>
    public string? Caller { get; set; }

    /// <summary>The configuration section naming the owner's api version per service; a service without an entry is <c>v1</c>.</summary>
    public const string ApiVersionsSection = "InternalApiVersions";

    /// <summary>The configuration section naming the host per service, shared with the internal client.</summary>
    public const string HostsSection = "InternalHosts";

    private const string DefaultApiVersion = "v1";

    /// <summary>The form of an <c>InternalHosts</c> value, as a log line and a refusal to start name it.</summary>
    public const string HostEntryForm =
        "host or host:port, where host is a name of letters, digits, dots, hyphens and underscores, an IPv4 address or an IPv6 address in brackets, and port a number from 1 to 65535; no scheme, no user, no path, no query";

    // A name may end in one dot: a fully qualified name, which a resolver does not complete with a search domain.

    /// <summary>
    /// What a configured host value has that a host with an optional port has not, or null for a
    /// value of the form (<see cref="HostEntryForm"/>). The answer never repeats the value, which
    /// may hold a credential.
    /// </summary>
    /// <param name="value">The trimmed configuration value.</param>
    internal static string? HostEntryDefect(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Any(char.IsWhiteSpace))
            return "it contains whitespace";

        if (value.Contains("://", StringComparison.Ordinal))
            return "it names a scheme";

        if (value.Contains('@'))
            return "it carries user information";

        if (value.IndexOfAny(['/', '\\', '?', '#']) >= 0)
            return "it has a path or a query";

        var match = HostEntry().Match(value);

        if (!match.Success)
            return "it holds a character a host name has none of";

        if (match.Groups["port"] is { Success: true, Value: var port }
            && (!int.TryParse(port, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) || number is < 1 or > 65535))
        {
            return "its port is no number from 1 to 65535";
        }

        // What the calls are sent to is the address this value forms: it has to form one, of this host and nothing more.
        return Uri.TryCreate($"http://{value}/", UriKind.Absolute, out var address) && address.UserInfo.Length == 0 && address.AbsolutePath == "/"
            ? null
            : "it forms no address";
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^(?:[A-Za-z0-9_](?:[A-Za-z0-9._-]*[A-Za-z0-9_])?\.?|\[[0-9A-Fa-f:.]+\])(?::(?<port>[0-9]*))?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex HostEntry();

    /// <summary>
    /// The configuration key of the time the named client waits for an owner to accept a
    /// connection, in milliseconds. Zero or less: the client sets no connect timeout of its own,
    /// and a call waits for a connection as long as its budget lasts.
    /// </summary>
    public const string ConnectTimeoutKey = "OxQL:Remote:ConnectTimeoutMs";

    /// <summary>
    /// How long the named client waits for an owner to accept a connection where the host
    /// configures nothing under <see cref="ConnectTimeoutKey"/>. An owner that accepts none
    /// within it fails the call as an unreachable owner, without waiting for the call's budget;
    /// a call on an established connection is bounded by its budget alone.
    /// </summary>
    public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The connect timeout a host configures: <see cref="ConnectTimeoutKey"/>, else
    /// <see cref="DefaultConnectTimeout"/>; <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>
    /// for a value of zero or less. A value that is no whole number throws, which stops the start.
    /// </summary>
    internal static TimeSpan ConnectTimeoutOf(IConfiguration configuration) =>
        configuration.GetValue<int?>(ConnectTimeoutKey) switch
        {
            null => DefaultConnectTimeout,
            <= 0 => System.Threading.Timeout.InfiniteTimeSpan,
            var milliseconds => TimeSpan.FromMilliseconds(milliseconds.Value),
        };

    /// <summary>
    /// Registers the named client every owner call goes through, with the host's connect
    /// timeout (<see cref="ConnectTimeoutOf"/>, read here, once) on its connections, and which
    /// follows no redirect. The client's own timeout stays the default: every call brings its
    /// budget as a token, and none waits on the client's.
    /// </summary>
    internal static IHttpClientBuilder AddHttpClient(IServiceCollection services, IConfiguration configuration)
    {
        var connectTimeout = ConnectTimeoutOf(configuration);

        return services.AddHttpClient(HttpClientName).UseSocketsHttpHandler((handler, _) =>
        {
            handler.ConnectTimeout = connectTimeout;

            // A call carries the internal key and the caller's identity to the configured owner, and to nobody an answer points at.
            handler.AllowAutoRedirect = false;
        });
    }

    /// <summary>
    /// The bound of a call whose caller names no positive budget: the query engine's default
    /// ceiling for one request. No outbound call waits on the HTTP client's own timeout.
    /// </summary>
    internal TimeSpan FallbackBudget { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>The bound of one shallow-health read: the host health probe's own timeout.</summary>
    internal TimeSpan HealthBudget { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long what an owner's shallow health said of it stays known, and how long a first-use
    /// probe that learned nothing waits before it is tried again: the health probe's time to live
    /// (<c>OxQL:Cache:HealthProbeTtlSeconds</c>).
    /// </summary>
    internal TimeSpan FactsTtl { get; set; }

    /// <summary>The clock the owner facts age by, and the one the time bounds of the remote calls run on.</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>The most of an owner's refusal message a thrown exception quotes.</summary>
    private const int RefusalMessageLimit = 500;

    private readonly IHttpClientFactory clients;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<RemoteQueryClient> logger;
    private readonly string internalApiKey;
    private readonly IReadOnlyDictionary<string, string> hosts;
    private readonly IReadOnlyDictionary<string, string> refusedHosts;
    private readonly IReadOnlyDictionary<string, string> versions;
    private readonly TimeSpan connectTimeout;
    private readonly ConcurrentDictionary<string, OwnerFacts> owners = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> probed = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<Task>> probing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What an owner's shallow health said of it, and when it was read.</summary>
    private sealed record OwnerFacts(RemoteOwnerInfo Info, DateTimeOffset ReadAt);

    /// <summary>
    /// Creates the client. The hosts, the api versions and the internal key are read once,
    /// here; a change to the configuration needs a restart, as it does for the internal client.
    /// </summary>
    /// <param name="clients">The factory of the named client every call goes through.</param>
    /// <param name="auth">The settings holding the internal api key.</param>
    /// <param name="configuration">The host's configuration, for <see cref="HostsSection"/>, <see cref="ApiVersionsSection"/> and <see cref="ConnectTimeoutKey"/>.</param>
    /// <param name="httpContextAccessor">The current request, whose scope provider names the identity to forward.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="oxql">The query engine's options, for the health probe's time to live; the engine's default without them.</param>
    public RemoteQueryClient(
        IHttpClientFactory clients,
        IOptions<AuthSettings> auth,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RemoteQueryClient> logger,
        OxQLOptions? oxql = null)
    {
        this.clients = clients ?? throw new ArgumentNullException(nameof(clients));
        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(configuration);

        internalApiKey = auth.Value.InternalApiKey;
        var configured = Map(configuration, HostsSection);

        // Only a value of the form is ever called; the others are kept by what is wrong with them.
        hosts = configured.Where(entry => HostEntryDefect(entry.Value) is null).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        refusedHosts = configured.Where(entry => !hosts.ContainsKey(entry.Key)).ToDictionary(entry => entry.Key, entry => HostEntryDefect(entry.Value)!, StringComparer.OrdinalIgnoreCase);
        versions = Map(configuration, ApiVersionsSection);
        connectTimeout = ConnectTimeoutOf(configuration);
        FactsTtl = TimeSpan.FromSeconds(Math.Max(1, (oxql ?? new OxQLOptions()).Cache.HealthProbeTtlSeconds));
    }

    /// <summary>
    /// The api version segment the owner of a service key answers on: its
    /// <see cref="ApiVersionsSection"/> entry, else <c>v1</c>; null when the host does not know the
    /// service. What an explain answer names as the owner's route version.
    /// </summary>
    public string? ApiVersionOf(string serviceKey)
    {
        if (serviceKey is null || !hosts.ContainsKey(serviceKey))
            return null;

        return versions.TryGetValue(serviceKey, out var configured) && !string.IsNullOrWhiteSpace(configured) ? configured : DefaultApiVersion;
    }

    /// <summary>
    /// Where <see cref="ApiVersionOf"/> has the version of a service key from: the configuration
    /// key <c>InternalApiVersions:{service}</c> when the host has a non-blank entry, else
    /// <c>default</c>; null when the host does not know the service.
    /// </summary>
    public string? ApiVersionSourceOf(string serviceKey)
    {
        if (serviceKey is null || !hosts.ContainsKey(serviceKey))
            return null;

        return versions.TryGetValue(serviceKey, out var configured) && !string.IsNullOrWhiteSpace(configured) ? $"{ApiVersionsSection}:{serviceKey}" : "default";
    }

    /// <summary>
    /// The owner's base route for a service key, <c>http://{host}/{ns}-api/{version}/</c> (the
    /// version is <see cref="ApiVersionOf"/>), or null when the host does not know the service.
    /// Every owner route this client calls is a path under it.
    /// </summary>
    public string? RouteOf(string serviceKey) =>
        serviceKey is not null && hosts.TryGetValue(serviceKey, out var host) ? $"http://{host}/{serviceKey}-api/{ApiVersionOf(serviceKey)}/" : null;

    /// <summary>The owner's batch route for a service key, or null when the host does not know the service.</summary>
    public string? BatchUrl(string serviceKey) => Url(serviceKey, "internal/oxql/batch");

    /// <summary>The owner's internal explain route for a service key, or null when the host does not know the service.</summary>
    public string? ExplainUrl(string serviceKey) => Url(serviceKey, "internal/oxql/explain");

    /// <summary>
    /// The owner's health route for a service key, or null when the host does not know the
    /// service. The shallow form: the owner answers without its own remote state and starts no
    /// measurement of its own, so a probe never sets off the probed service's probes.
    /// </summary>
    public string? HealthUrl(string serviceKey) => Url(serviceKey, "OxQL/health?shallow=true");

    /// <summary>
    /// Whether the host has an <see cref="HostsSection"/> entry of the form for the service
    /// (<see cref="HostEntryForm"/>). A service whose entry is of another form is not configured.
    /// </summary>
    /// <inheritdoc/>
    public bool IsConfigured(string serviceKey) => serviceKey is not null && hosts.ContainsKey(serviceKey);

    /// <summary>
    /// What the <see cref="HostsSection"/> entry of a service has that a host with an optional
    /// port has not (one short clause, which never repeats the value), or null for a service whose entry is of the
    /// form or that has none. Such a service is not configured and never called.
    /// </summary>
    public string? RefusedHostOf(string serviceKey) =>
        serviceKey is not null && refusedHosts.TryGetValue(serviceKey, out var defect) ? defect : null;

    /// <summary>
    /// Executes a batch at the owner. The body is <c>queries</c> and <c>maxTimeMs</c> only (the
    /// owner refuses any other member); <c>maxTimeMs</c> is the owner's batch-wide ceiling
    /// (<see cref="Ceiling"/>): the smaller of the one the engine wrote, which the engine already
    /// sets below its own wait, and the time this call is given. The engine sizes the batch by the
    /// owner's cap (<see cref="OwnerOfAsync"/>); the client sends it as it is.
    /// </summary>
    /// <inheritdoc/>
    public async Task<BatchResponse> BatchAsync(string serviceKey, BatchRequest request, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var address = AddressOf(serviceKey, BatchUrl(serviceKey), "batch");
        var bound = budget > TimeSpan.Zero ? budget : FallbackBudget;
        var body = new BatchRequest { Queries = request.Queries, MaxTimeMs = Ceiling(request.MaxTimeMs, bound) };

        using var timeout = Timeout(bound, cancellationToken);
        using var response = await SendAsync(serviceKey, address, body, "batch", timeout.Token, cancellationToken);

        return await response.Content.ReadFromJsonAsync<BatchResponse>(OxQLJson.Wire, timeout.Token)
            ?? throw new HttpRequestException($"The owner of '{serviceKey}' answered the internal batch with an empty body.");
    }

    /// <summary>
    /// Explains one request at the owner: a batch of one check over its internal explain route
    /// (<see cref="ExplainBatchAsync"/>), with the request's own budget as the batch's. Null when the
    /// owner left the check unanswered.
    /// </summary>
    /// <inheritdoc/>
    public async Task<JsonObject?> ExplainAsync(string serviceKey, ExplainRequest request, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var answers = await ExplainBatchAsync(serviceKey, new ExplainBatchRequest { Checks = [request with { Budget = null }], Budget = request.Budget }, budget, cancellationToken);

        return answers is { Count: > 0 } ? answers[0] : null;
    }

    /// <summary>
    /// Explains the checks of one round of an explain at the owner, in one call: the body is
    /// <c>{ checks, budget }</c>, the answer <c>{ answers }</c> with one entry per check, an entry null
    /// where the owner left that check unanswered. An owner that answers anything but 200 (404 while its
    /// explain is switched off, 401 on a wrong key, 429 from its limiter, 400 for a batch past its bound)
    /// throws, as an unreachable or timed-out owner does, and none of the checks is answered.
    /// </summary>
    /// <inheritdoc/>
    public async Task<IReadOnlyList<JsonObject?>?> ExplainBatchAsync(string serviceKey, ExplainBatchRequest batch, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var address = AddressOf(serviceKey, ExplainUrl(serviceKey), "explain");
        var bound = budget > TimeSpan.Zero ? budget : FallbackBudget;

        using var timeout = Timeout(bound, cancellationToken);
        using var response = await SendAsync(serviceKey, address, batch, "explain", timeout.Token, cancellationToken);

        var answered = await response.Content.ReadFromJsonAsync<ExplainBatchResponse>(OxQLJson.Wire, timeout.Token)
            ?? throw new HttpRequestException($"The owner of '{serviceKey}' answered the internal explain without an answer object.");

        if (answered.Answers.Count != batch.Checks.Count)
            throw new HttpRequestException($"The owner of '{serviceKey}' answered {answered.Answers.Count} of the {batch.Checks.Count} checks of the internal explain.");

        return answered.Answers.Select(answer => answer as JsonObject).ToList();
    }

    /// <summary>
    /// What the owner's shallow health last said of it, or null while unknown: never read, or
    /// read longer ago than <see cref="FactsTtl"/>, so a rolled-back owner is not taken for the
    /// engine it ran before.
    /// </summary>
    /// <inheritdoc/>
    public RemoteOwnerInfo? OwnerOf(string serviceKey) =>
        serviceKey is not null && owners.TryGetValue(serviceKey, out var facts) && Time.GetUtcNow() - facts.ReadAt < FactsTtl ? facts.Info : null;

    /// <summary>
    /// Whether the owner's shallow health answers; a success also keeps what that health says of
    /// the owner, for <see cref="OwnerOf"/>. An owner that answers 404 is reached and does not
    /// serve the route where this host calls it (its api version, its host): that is thrown as
    /// an <see cref="HttpRequestException"/> with the status, as a batch and an explain throw
    /// it, so whoever asks can tell a route that is not there from an owner that is down.
    /// </summary>
    /// <exception cref="HttpRequestException">The owner answered 404 to its health route.</exception>
    /// <inheritdoc/>
    public async Task<bool> IsReachableAsync(string serviceKey, CancellationToken cancellationToken)
    {
        if (HealthUrl(serviceKey) is not { } url)
            return false;

        // Also a host entry that forms no address counts as tried, so OwnerOfAsync does not probe
        // (and log) it again on every batch within the TTL.
        probed[serviceKey] = Time.GetUtcNow();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
            return false;

        using var timeout = Timeout(HealthBudget, cancellationToken);

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, address);
            using var response = await clients.CreateClient(HttpClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new HttpRequestException(
                    $"The owner of '{serviceKey}' answered 404 to its health route: it does not serve the query routes at the configured address.",
                    null,
                    System.Net.HttpStatusCode.NotFound);
            }

            if (!response.IsSuccessStatusCode)
                return false;

            await RememberAsync(serviceKey, response, timeout.Token, cancellationToken);

            return true;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested && exception is not HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
        {
            return false;
        }
    }

    /// <summary>
    /// What is known of the owner before the engine sends it a request's first batch. When its
    /// facts are unknown or stale and no probe was tried within <see cref="FactsTtl"/>, its shallow
    /// health is read first; concurrent callers share one probe, which runs on when one caller
    /// stops waiting. The probe is bounded by the health budget and never fails the caller: an
    /// owner it cannot read stays unknown (null), and the batch that follows reports it as it
    /// would have.
    /// </summary>
    /// <inheritdoc/>
    public async ValueTask<RemoteOwnerInfo?> OwnerOfAsync(string serviceKey, CancellationToken cancellationToken)
    {
        if (!IsConfigured(serviceKey))
            return null;

        if (OwnerOf(serviceKey) is { } known)
            return known;

        if (IsProbeFresh(serviceKey))
            return null;

        await probing.GetOrAdd(serviceKey, key => new Lazy<Task>(() => ProbeAsync(key))).Value.WaitAsync(cancellationToken);

        return OwnerOf(serviceKey);
    }

    /// <summary>Whether the owner's shallow health was tried within <see cref="FactsTtl"/>, whatever it answered.</summary>
    internal bool IsProbeFresh(string serviceKey) =>
        probed.TryGetValue(serviceKey, out var tried) && Time.GetUtcNow() - tried < FactsTtl;

    /// <summary>
    /// One shallow-health probe on its own clock, so no one caller's cancellation ends it for the
    /// others; it leaves the shared slot when it ends, so the next stale read probes anew.
    /// </summary>
    private async Task ProbeAsync(string serviceKey)
    {
        try
        {
            if (!await IsReachableAsync(serviceKey, CancellationToken.None))
                logger.LogInformation("OxQL owner {Service} did not answer its health before a batch; its engine facts stay unknown", serviceKey);
        }
        catch (HttpRequestException)
        {
            // The owner does not serve the route: the batch that follows meets the same and reports it.
            logger.LogInformation("OxQL owner {Service} did not answer its health before a batch; its engine facts stay unknown", serviceKey);
        }
        finally
        {
            probing.TryRemove(serviceKey, out _);
        }
    }

    /// <summary>
    /// The owner's batch-wide ceiling in whole milliseconds (at least one): the smaller positive of
    /// the engine's ceiling and the call's bound. No second margin: the engine already writes its
    /// ceiling a tenth (at most 250 ms) below the time it waits.
    /// </summary>
    internal static int Ceiling(int? requested, TimeSpan bound)
    {
        var available = (int)Math.Clamp(Math.Ceiling(bound.TotalMilliseconds), 1, int.MaxValue);

        return requested is > 0 and var value ? Math.Min(value, available) : available;
    }

    /// <summary>
    /// Keeps what the owner's shallow health says of it. A body that is not the health answer,
    /// or that could not be read in time, leaves what was known before: the owner answered, so it
    /// is reachable either way.
    /// </summary>
    private async Task RememberAsync(string serviceKey, HttpResponseMessage response, CancellationToken bounded, CancellationToken cancellationToken)
    {
        try
        {
            var health = await response.Content.ReadFromJsonAsync<JsonNode>(bounded);

            if (RemoteOwnerInfo.FromShallowHealth(health) is { } info)
                owners[serviceKey] = new OwnerFacts(info, Time.GetUtcNow());
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "OxQL owner {Service} answered its health with a body that is not JSON; its engine facts stay as they were", serviceKey);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "OxQL owner {Service} answered its health, but its body could not be read; its engine facts stay as they were", serviceKey);
        }
    }

    private string? Url(string serviceKey, string path) => RouteOf(serviceKey) is { } route ? route + path : null;

    /// <summary>
    /// The address of an owner route. A service that is not configured is a caller error, also
    /// one whose host entry is not of the form (<see cref="RefusedHostOf"/>): nothing is sent.
    /// </summary>
    private Uri AddressOf(string serviceKey, string? url, string route)
    {
        if (url is null && RefusedHostOf(serviceKey) is { } defect)
            throw new InvalidOperationException($"The '{HostsSection}' entry for '{serviceKey}' is not used, because {defect}; the service is not configured on this host.");

        if (url is null)
            throw new InvalidOperationException($"No '{HostsSection}' entry for '{serviceKey}'; the service is not configured on this host.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
        {
            logger.LogWarning("OxQL remote {Route} to {Service} was not sent: its '{Section}' entry does not form a valid address", route, serviceKey, HostsSection);

            throw new HttpRequestException($"The '{HostsSection}' entry for '{serviceKey}' does not form a valid address.");
        }

        return address;
    }

    /// <summary>
    /// Posts <paramref name="body"/> with the forwarded headers, cancelled by <paramref name="bounded"/>;
    /// an answer other than a success throws, quoting the owner's refusal code and message when
    /// its body is a refusal. The caller disposes the response.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync<T>(string serviceKey, Uri address, T body, string route, CancellationToken bounded, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, address)
        {
            Content = JsonContent.Create(body, options: OxQLJson.Wire),
        };

        await ForwardAsync(message, cancellationToken);

        HttpResponseMessage response;

        try
        {
            response = await clients.CreateClient(HttpClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, bounded);
        }
        catch (OperationCanceledException exception) when (exception.InnerException is TimeoutException && !bounded.IsCancellationRequested)
        {
            // The handler gave up connecting (its connect timeout) while the call still had budget:
            // nobody accepted the connection, which is an owner that cannot be reached and not a
            // call that ran out of time.
            logger.LogWarning("OxQL remote {Route} to {Service} was not sent: the owner accepted no connection within {ConnectTimeoutMs} ms", route, serviceKey, (int)connectTimeout.TotalMilliseconds);

            throw new HttpRequestException($"The owner of '{serviceKey}' accepted no connection for the internal {route} in time.", exception);
        }

        if (response.IsSuccessStatusCode)
            return response;

        var status = response.StatusCode;
        var (code, detail) = await RefusalOfAsync(response, bounded, cancellationToken);

        response.Dispose();
        logger.LogWarning("OxQL remote {Route} to {Service} answered {Status} {Code}", route, serviceKey, (int)status, code);

        // The named client follows no redirect, so the route is not served where the call went:
        // the engine is told what it is told of an owner that answers 404 there.
        if ((int)status is >= 300 and < 400)
        {
            throw new HttpRequestException(
                $"The owner of '{serviceKey}' answered the internal {route} with a redirect ({(int)status}), which is not followed: it does not serve the route at the configured address.",
                null,
                System.Net.HttpStatusCode.NotFound);
        }

        var refusal = code is null ? "." : detail is null ? $": {code}." : $": {code} {detail}";

        throw new HttpRequestException($"The owner of '{serviceKey}' answered {(int)status} to the internal {route}{refusal}", null, status);
    }

    /// <summary>
    /// The code and message of the owner's refusal body (<c>{ type, title, errors: [{ code, message }] }</c>):
    /// the first error's, else the reason class and title; nulls when the body is none or cannot
    /// be read in time. The message is cut to <see cref="RefusalMessageLimit"/> characters.
    /// </summary>
    private static async Task<(string? Code, string? Message)> RefusalOfAsync(HttpResponseMessage response, CancellationToken bounded, CancellationToken cancellationToken)
    {
        try
        {
            if (await response.Content.ReadFromJsonAsync<JsonNode>(bounded) is not JsonObject body)
                return (null, null);

            var error = body["errors"] is JsonArray { Count: > 0 } errors ? errors[0] as JsonObject : null;
            var code = Text(error?["code"]) ?? Text(body["type"]);
            var text = Text(error?["message"]) ?? Text(body["title"]);

            return (code, text is { Length: > RefusalMessageLimit } ? text[..RefusalMessageLimit] : text);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, null);
        }

        static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    }

    /// <summary>
    /// A token cancelled by <paramref name="cancellationToken"/> or once <paramref name="bound"/>
    /// has passed on <see cref="Time"/>.
    /// </summary>
    private Bound Timeout(TimeSpan bound, CancellationToken cancellationToken) => new(bound, Time, cancellationToken);

    /// <summary>A caller's token linked to a time bound on a clock; disposing it stops the bound's timer.</summary>
    private sealed class Bound : IDisposable
    {
        private readonly CancellationTokenSource elapsed;
        private readonly CancellationTokenSource linked;

        public Bound(TimeSpan bound, TimeProvider time, CancellationToken cancellationToken)
        {
            elapsed = new CancellationTokenSource(bound, time);
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, elapsed.Token);
        }

        public CancellationToken Token => linked.Token;

        public void Dispose()
        {
            linked.Dispose();
            elapsed.Dispose();
        }
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
    /// <para>
    /// The key and the three identity headers are the shared internal mechanism's, not OxQL's:
    /// <c>Simplic.OxS.InternalClient.InternalClientBase</c> (its constructor and
    /// <c>SetRequestHeader</c>) is the reference for that set. This client is a second sender of
    /// it only because the engine calls owners in parallel, with a token and a budget per call
    /// (OX_SCHEMA.md, section 4.2). Whatever the shared client starts or stops forwarding has to
    /// be mirrored here; the contract and caller headers are this client's own and no identity.
    /// </para>
    /// </summary>
    private async Task ForwardAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue(Constants.HttpAuthorizationSchemeInternalKey, internalApiKey);
        message.Headers.TryAddWithoutValidation(OxQLQueryService.ContractHeader, EngineCapabilities.Contract.ToString());

        if (!string.IsNullOrWhiteSpace(Caller))
            message.Headers.TryAddWithoutValidation(CallerHeader, Caller);

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

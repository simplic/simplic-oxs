using Simplic.OxS.ABAC.Sync;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// ABAC runtime configuration, bound from the configuration section <c>Abac</c>.
/// </summary>
public class AbacRuntimeOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Abac";

    /// <summary>Rule-set sync settings (<c>Abac:Sync:*</c>).</summary>
    public AbacSyncOptions Sync { get; set; } = new();

    /// <summary>L1 decision cache settings (<c>Abac:L1:*</c>).</summary>
    public AbacL1Options L1 { get; set; } = new();
}

/// <summary>
/// Service-local rule store sync settings (§12.6), section <c>Abac:Sync</c>.
/// </summary>
public class AbacSyncOptions
{
    /// <summary><c>Abac:Sync:Enabled</c> — enables the periodic background re-sync job. Default <c>true</c>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary><c>Abac:Sync:Interval</c> — period of the background re-sync job. Default 5 minutes.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary><c>Abac:Sync:MaxAge</c> — a set older than this is re-pulled and reported as stale. Default 1 hour.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(1);

    /// <summary><c>Abac:Sync:HardLimit</c> — a set older than this is no longer used (→ DENY). Default 24 hours.</summary>
    public TimeSpan HardLimit { get; set; } = TimeSpan.FromHours(24);

    /// <summary><c>Abac:Sync:PullTimeout</c> — timeout of the synchronous on-demand pull on a cache/store miss. Default 3 seconds.</summary>
    public TimeSpan PullTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary><c>Abac:Sync:FailureBackoff</c> — after a failed on-demand pull, no further on-demand pull for this org within this period. Default 30 seconds.</summary>
    public TimeSpan FailureBackoff { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary><c>Abac:Sync:RedisTtl</c> — TTL of the Redis hot-cache entries written by the sync. Default 60 minutes.</summary>
    public TimeSpan RedisTtl { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>
    /// <c>Abac:Sync:ActionPrefixes</c> — optional action prefixes this service serves (e.g. <c>oxs:contact:</c>).
    /// The full set is stored; when reading, rules whose action patterns cannot match any prefix are dropped
    /// (wildcards like <c>*</c> / <c>oxs:*</c> are kept). Accepts an array or a comma-separated string.
    /// Empty = no filtering.
    /// </summary>
    public string[] ActionPrefixes { get; set; } = [];

    /// <summary><c>Abac:Sync:Service</c> — key in <c>InternalHosts</c> and service segment of the pull URL. Default <c>identity</c>.</summary>
    public string Service { get; set; } = AbacSyncEndpoints.Service;

    /// <summary>
    /// <c>Abac:Sync:BaseUrl</c> — optional absolute URL override of the pull endpoint. When empty the URL is built like
    /// <c>InternalClientBase.BuildUrl</c>: <c>{Scheme}://{InternalHosts[Service]}/{Service}-api/{ApiVersion}/internal/AbacPolicySync/rule-set</c>.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary><c>Abac:Sync:Scheme</c> — scheme for the internal pull request. Default <c>http</c>.</summary>
    public string Scheme { get; set; } = "http";

    /// <summary><c>Abac:Sync:ApiVersion</c> — API version segment for the internal pull request. Default <c>v1</c>.</summary>
    public string ApiVersion { get; set; } = "v1";

    /// <summary>Returns <see cref="ActionPrefixes"/> normalized (comma-split, trimmed, non-empty).</summary>
    public IReadOnlyList<string> GetNormalizedActionPrefixes()
        => ActionPrefixes
            .SelectMany(p => (p ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

/// <summary>
/// L1 in-memory decision cache settings, section <c>Abac:L1</c>.
/// </summary>
public class AbacL1Options
{
    /// <summary><c>Abac:L1:Enabled</c> — enables the L1 decision cache. Default <c>true</c>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary><c>Abac:L1:Ttl</c> — TTL of L1 entries. Default 30 seconds. Entries are keyed by set id and thus invalidated implicitly on activation.</summary>
    public TimeSpan Ttl { get; set; } = TimeSpan.FromSeconds(30);
}

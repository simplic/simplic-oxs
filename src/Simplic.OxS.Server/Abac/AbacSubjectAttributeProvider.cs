using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.ABAC.Cache;
using Simplic.OxS.ABAC.Sync;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// Resolves the subject's live roles/teams for (user, organisation) server-side, with a short-TTL cache.
/// </summary>
public interface IAbacSubjectAttributeProvider
{
    /// <summary>
    /// Returns the attributes of <paramref name="userId"/> within <paramref name="organizationId"/>, or <c>null</c> when
    /// the user is unknown or not a member of the organisation. Throws when the attributes cannot be resolved
    /// (Identity unavailable, timeout, invalid answer) — callers must treat that as "no subject" (DENY).
    /// </summary>
    Task<AbacSubjectAttributes?> GetAttributesAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
}

/// <summary>
/// Cached <see cref="IAbacSubjectAttributeProvider"/> on top of <see cref="IAbacSubjectAttributeClient"/>.
/// <list type="number">
///   <item>L1 <see cref="IMemoryCache"/> → L2 Redis (<see cref="IDistributedCache"/>, key <see cref="AbacCacheKeys.SubjectAttributes"/>)
///   → Identity lookup (timeout <c>Abac:Subject:Timeout</c>).</item>
///   <item>Results — including "not a member" — are cached for <c>Abac:Subject:Ttl</c> (default 30 s). Both levels share the
///   same absolute expiry, so the TTL is the upper bound of the revocation lag of role/team changes.</item>
///   <item>Failures are never cached and propagate to the caller (fail closed). Redis errors are logged and skipped.</item>
/// </list>
/// </summary>
public class AbacSubjectAttributeProvider : IAbacSubjectAttributeProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAbacSubjectAttributeClient _client;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AbacSubjectAttributeProvider> _logger;
    private readonly AbacSubjectOptions _options;
    private readonly IDistributedCache? _distributedCache;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of <see cref="AbacSubjectAttributeProvider"/>.</summary>
    public AbacSubjectAttributeProvider(
        IAbacSubjectAttributeClient client,
        IMemoryCache memoryCache,
        ILogger<AbacSubjectAttributeProvider> logger,
        IOptions<AbacRuntimeOptions>? options = null,
        IDistributedCache? distributedCache = null,
        TimeProvider? timeProvider = null)
    {
        _client = client;
        _memoryCache = memoryCache;
        _logger = logger;
        _options = options?.Value.Subject ?? new AbacSubjectOptions();
        _distributedCache = distributedCache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<AbacSubjectAttributes?> GetAttributesAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var key = AbacCacheKeys.SubjectAttributes(organizationId, userId);
        var cacheEnabled = _options.Ttl > TimeSpan.Zero;

        if (cacheEnabled)
        {
            var now = _timeProvider.GetUtcNow();
            if (_memoryCache.TryGetValue(key, out CacheEntry? local) && local != null && local.ExpiresAt > now)
                return local.Attributes;

            var shared = await ReadDistributedAsync(key, now, ct);
            if (shared != null)
            {
                _memoryCache.Set(key, shared, shared.ExpiresAt);
                return shared.Attributes;
            }
        }

        AbacSubjectAttributes? attributes;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            if (_options.Timeout > TimeSpan.Zero)
                timeout.CancelAfter(_options.Timeout);

            attributes = await _client.GetAttributesAsync(userId, organizationId, timeout.Token);
        }

        if (attributes != null)
        {
            attributes.Roles ??= [];
            attributes.Teams ??= [];
        }

        if (cacheEnabled)
        {
            var entry = new CacheEntry { Attributes = attributes, ExpiresAt = _timeProvider.GetUtcNow() + _options.Ttl };
            _memoryCache.Set(key, entry, entry.ExpiresAt);
            await WriteDistributedAsync(key, entry, ct);
        }

        return attributes;
    }

    private async Task<CacheEntry?> ReadDistributedAsync(string key, DateTimeOffset now, CancellationToken ct)
    {
        if (_distributedCache == null)
            return null;

        try
        {
            var json = await _distributedCache.GetStringAsync(key, ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var entry = JsonSerializer.Deserialize<CacheEntry>(json, JsonOptions);
            return entry != null && entry.ExpiresAt > now ? entry : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "ABAC: Reading subject attributes '{Key}' from Redis failed — falling back to Identity.", key);
            return null;
        }
    }

    private async Task WriteDistributedAsync(string key, CacheEntry entry, CancellationToken ct)
    {
        if (_distributedCache == null)
            return;

        try
        {
            await _distributedCache.SetStringAsync(
                key,
                JsonSerializer.Serialize(entry, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpiration = entry.ExpiresAt },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "ABAC: Writing subject attributes '{Key}' to Redis failed.", key);
        }
    }

    /// <summary>Cached lookup result; <see cref="Attributes"/> <c>null</c> = not a member.</summary>
    internal sealed class CacheEntry
    {
        public AbacSubjectAttributes? Attributes { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }
    }
}

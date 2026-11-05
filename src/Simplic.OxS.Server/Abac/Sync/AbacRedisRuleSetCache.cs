using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Simplic.OxS.ABAC.Cache;
using Simplic.OxS.ABAC.Compiled;
using Simplic.OxS.ABAC.Sync;

namespace Simplic.OxS.Server.Abac.Sync;

/// <summary>
/// Flags stored next to an active set in Redis under <see cref="AbacCacheKeys.Flags"/>
/// (<c>abac:policy:{orgId}:flags:{setId}</c>), JSON: <c>{"isConfirmedEmpty":bool,"allowAllWhenEmpty":bool}</c>.
/// </summary>
public class AbacRuleSetFlags
{
    /// <summary>Identity confirmed that the organization has no policies.</summary>
    public bool IsConfirmedEmpty { get; set; }

    /// <summary>Effective rollout flag (§6.4).</summary>
    public bool AllowAllWhenEmpty { get; set; }
}

/// <summary>
/// Reads and writes an organization's rule set in the Redis hot cache using the same key scheme
/// as Identity (<see cref="AbacCacheKeys"/>):
/// <list type="bullet">
///   <item><c>abac:policy:{org}:active-set</c> → set id (plain string)</item>
///   <item><c>abac:policy:{org}:index:{engine}:{setId}</c> → JSON array of policy ids</item>
///   <item><c>abac:policy:{org}:{policyId}:compiled:{engine}:{setId}</c> → compiled policy JSON</item>
///   <item><c>abac:policy:{org}:flags:{setId}</c> → <see cref="AbacRuleSetFlags"/> JSON</item>
/// </list>
/// </summary>
public static class AbacRedisRuleSetCache
{
    /// <summary>Reads the active-set pointer, or <c>null</c> when absent.</summary>
    public static async Task<string?> ReadActiveSetIdAsync(IDistributedCache cache, Guid organizationId, CancellationToken ct)
    {
        var setId = await cache.GetStringAsync(AbacCacheKeys.ActiveSet(organizationId), ct);
        return string.IsNullOrWhiteSpace(setId) ? null : setId.Trim();
    }

    /// <summary>
    /// Reads the complete set <paramref name="setId"/>. Returns <c>null</c> (cache miss) when the index is
    /// absent (and the set is not flagged as confirmed empty), cannot be parsed, or any listed policy is
    /// missing/unreadable — a partial set must never be evaluated (a missing deny policy would leak data).
    /// </summary>
    public static async Task<AbacOrganizationRuleSet?> ReadSetAsync(IDistributedCache cache, Guid organizationId, string setId, ILogger logger, CancellationToken ct)
    {
        var flags = await ReadFlagsAsync(cache, organizationId, setId, logger, ct);

        var indexJson = await cache.GetStringAsync(AbacCacheKeys.PolicyIndex(organizationId, AbacEngineVersion.Current, setId), ct);
        if (string.IsNullOrWhiteSpace(indexJson))
        {
            if (flags?.IsConfirmedEmpty == true)
                return NewSet(organizationId, setId, flags, []);

            logger.LogDebug("ABAC: No policy index in Redis for org '{OrgId}', set '{SetId}'.", organizationId, setId);
            return null;
        }

        List<string>? policyIds;
        try
        {
            policyIds = AbacRuleSetJson.Deserialize<List<string>>(indexJson);
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogError(ex, "ABAC: Failed to deserialize policy index for org '{OrgId}'.", organizationId);
            return null;
        }

        if (policyIds == null)
            return null;

        var policies = new List<CompiledPolicy>(policyIds.Count);
        foreach (var policyId in policyIds)
        {
            var policyJson = await cache.GetStringAsync(
                AbacCacheKeys.CompiledPolicy(organizationId, policyId, AbacEngineVersion.Current, setId), ct);

            if (string.IsNullOrWhiteSpace(policyJson))
            {
                logger.LogWarning(
                    "ABAC: Policy '{PolicyId}' listed in index but missing from Redis (org '{OrgId}', set '{SetId}') — treating as cache miss.",
                    policyId, organizationId, setId);
                return null;
            }

            try
            {
                var policy = AbacRuleSetJson.Deserialize<CompiledPolicy>(policyJson);
                if (policy == null)
                    return null;
                policies.Add(policy);
            }
            catch (System.Text.Json.JsonException ex)
            {
                logger.LogError(ex, "ABAC: Failed to deserialize policy '{PolicyId}' for org '{OrgId}' — treating as cache miss.", policyId, organizationId);
                return null;
            }
        }

        return NewSet(organizationId, setId, flags, policies);
    }

    /// <summary>
    /// Writes the set (policies, index, flags) and finally switches the active-set pointer, so readers
    /// never observe a pointer to an incomplete set.
    /// </summary>
    public static async Task WriteAsync(IDistributedCache cache, AbacOrganizationRuleSet set, TimeSpan ttl, CancellationToken ct)
    {
        var engine = string.IsNullOrWhiteSpace(set.EngineVersion) ? AbacEngineVersion.Current : set.EngineVersion;
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        var org = set.OrganizationId;
        var policies = set.Policies ?? [];

        foreach (var policy in policies)
        {
            await cache.SetStringAsync(
                AbacCacheKeys.CompiledPolicy(org, policy.PolicyId, engine, set.SetId),
                AbacRuleSetJson.Serialize(policy), options, ct);
        }

        await cache.SetStringAsync(
            AbacCacheKeys.PolicyIndex(org, engine, set.SetId),
            AbacRuleSetJson.Serialize(policies.Select(p => p.PolicyId).ToList()), options, ct);

        await cache.SetStringAsync(
            AbacCacheKeys.Flags(org, set.SetId),
            AbacRuleSetJson.Serialize(new AbacRuleSetFlags
            {
                IsConfirmedEmpty = set.IsConfirmedEmpty && policies.Count == 0,
                AllowAllWhenEmpty = set.AllowAllWhenEmpty
            }), options, ct);

        await cache.SetStringAsync(AbacCacheKeys.ActiveSet(org), set.SetId, options, ct);
    }

    private static async Task<AbacRuleSetFlags?> ReadFlagsAsync(IDistributedCache cache, Guid organizationId, string setId, ILogger logger, CancellationToken ct)
    {
        var json = await cache.GetStringAsync(AbacCacheKeys.Flags(organizationId, setId), ct);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return AbacRuleSetJson.Deserialize<AbacRuleSetFlags>(json);
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogWarning(ex, "ABAC: Failed to deserialize rule-set flags for org '{OrgId}', set '{SetId}'.", organizationId, setId);
            return null;
        }
    }

    private static AbacOrganizationRuleSet NewSet(Guid organizationId, string setId, AbacRuleSetFlags? flags, List<CompiledPolicy> policies)
        => new()
        {
            OrganizationId = organizationId,
            SetId = setId,
            EngineVersion = AbacEngineVersion.Current,
            IsConfirmedEmpty = flags?.IsConfirmedEmpty ?? false,
            AllowAllWhenEmpty = flags?.AllowAllWhenEmpty ?? false,
            Policies = policies
        };
}

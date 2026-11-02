using System.Text.Json;
using System.Text.Json.Serialization;
using Simplic.OxS.ABAC.Compiled;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Sync;

namespace Simplic.OxS.Server.Abac.Sync;

/// <summary>
/// JSON conventions for ABAC rule sets exchanged with Identity, Redis and the local store.
/// Identity serializes with ASP.NET defaults (<see cref="JsonSerializerDefaults.Web"/>, camelCase,
/// enums as numbers). Reading is case-insensitive and accepts enums as numbers or strings.
/// </summary>
public static class AbacRuleSetJson
{
    /// <summary>Options used for reading (case-insensitive, enums as numbers or strings).</summary>
    public static JsonSerializerOptions ReadOptions { get; } = CreateReadOptions();

    /// <summary>Options used for writing (web defaults — identical to Identity).</summary>
    public static JsonSerializerOptions WriteOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes a value with <see cref="WriteOptions"/>.</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, WriteOptions);

    /// <summary>Deserializes a value written by Identity or this runtime.</summary>
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, ReadOptions);

    private static JsonSerializerOptions CreateReadOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>
/// Local relevance filtering of a rule set on the action prefixes served by this service (§12.6).
/// </summary>
public static class AbacActionPrefixFilter
{
    /// <summary>
    /// Returns <c>true</c> when at least one of <paramref name="patterns"/> can match an action that
    /// starts with one of <paramref name="prefixes"/>. Wildcard patterns (<c>*</c>, <c>oxs:*</c>) that
    /// cover a prefix are kept. With no prefixes configured every pattern is relevant.
    /// </summary>
    public static bool IsRelevant(IEnumerable<string> patterns, IReadOnlyList<string> prefixes)
    {
        if (prefixes.Count == 0)
            return true;

        foreach (var pattern in patterns)
        {
            var isWildcard = pattern.EndsWith('*');
            var stem = isWildcard ? pattern[..^1] : pattern;

            foreach (var prefix in prefixes)
            {
                if (stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (isWildcard && prefix.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Drops rules that are irrelevant for <paramref name="prefixes"/>; policies without remaining rules are dropped.
    /// The input objects are not modified.
    /// </summary>
    public static IReadOnlyList<CompiledPolicy> Apply(IReadOnlyList<CompiledPolicy> policies, IReadOnlyList<string> prefixes)
    {
        if (prefixes.Count == 0)
            return policies;

        var result = new List<CompiledPolicy>(policies.Count);
        foreach (var policy in policies)
        {
            var rules = policy.CompiledRules.Where(r => IsRelevant(r.ActionPatterns, prefixes)).ToList();
            if (rules.Count == 0)
                continue;

            result.Add(new CompiledPolicy
            {
                OrganizationId = policy.OrganizationId,
                PolicyId = policy.PolicyId,
                Description = policy.Description,
                CompiledRules = rules,
                CompiledAt = policy.CompiledAt,
                CompilerVersion = policy.CompilerVersion,
                SetId = policy.SetId
            });
        }

        return result;
    }
}

/// <summary>
/// Builds <see cref="AbacPolicySnapshot"/> instances from synchronized rule sets.
/// </summary>
public static class AbacSnapshotFactory
{
    /// <summary>
    /// Creates a snapshot. Only a set with <see cref="AbacOrganizationRuleSet.IsConfirmedEmpty"/> and
    /// no policies yields <see cref="AbacPolicySnapshotStatus.ConfirmedEmpty"/>; a set whose rules are all
    /// filtered away by the action prefixes stays <see cref="AbacPolicySnapshotStatus.Loaded"/> (→ implicit deny).
    /// </summary>
    public static AbacPolicySnapshot FromRuleSet(AbacOrganizationRuleSet set, AbacPolicySource source, bool isStale, IReadOnlyList<string> prefixes)
    {
        var policies = set.Policies ?? [];
        if (set.IsConfirmedEmpty && policies.Count == 0)
        {
            return new AbacPolicySnapshot
            {
                Status = AbacPolicySnapshotStatus.ConfirmedEmpty,
                Source = source,
                SetId = set.SetId,
                AllowAllWhenEmpty = set.AllowAllWhenEmpty,
                IsStale = isStale
            };
        }

        return new AbacPolicySnapshot
        {
            Status = AbacPolicySnapshotStatus.Loaded,
            Source = source,
            SetId = set.SetId,
            AllowAllWhenEmpty = set.AllowAllWhenEmpty,
            IsStale = isStale,
            Policies = AbacActionPrefixFilter.Apply(policies, prefixes)
        };
    }
}

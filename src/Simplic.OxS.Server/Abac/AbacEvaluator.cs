using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Simplic.OxS.ABAC.Cache;
using Simplic.OxS.ABAC.Compiled;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Subject;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// Default runtime ABAC evaluator (§12.7, §13.1).
/// <para>Shared gates (in this order):</para>
/// <list type="number">
///   <item>No authenticated subject → DENY.</item>
///   <item>Snapshot <see cref="AbacPolicySnapshotStatus.Unavailable"/> → DENY (fail closed).</item>
///   <item>Snapshot <see cref="AbacPolicySnapshotStatus.ConfirmedEmpty"/> → ALLOW (no filter) when the set carries
///   <c>AllowAllWhenEmpty</c> (rollout fallback §6.4), otherwise DENY.</item>
///   <item>Any policy compiled by another engine version → DENY with <see cref="AbacDecisionResult.IsVersionMismatch"/>.</item>
/// </list>
/// <para>Modes:</para>
/// <list type="bullet">
///   <item><b>Authorize (action level)</b>: ALLOW when an allow rule matches action+subject and no <i>unconditional</i>
///   deny rule matches. Deny rules with resource conditions do not block on action level — they are applied as
///   <c>$nor</c>/<c>must_not</c> filters or in the resource check.</item>
///   <item><b>Authorize (resource)</b>: evaluates resource conditions in-process (<see cref="AbacResourceConditionEvaluator"/>).</item>
///   <item><b>List filter</b>: <c>{"$and":[ allow-or, {"$nor":[deny…]} ]}</c>. Unexpressible deny → DENY; unexpressible allow → skipped.</item>
///   <item><b>Search filter</b>: <c>bool.filter</c> (allow) + <c>bool.must_not</c> (deny), same fail-closed rules.</item>
/// </list>
/// <para>
/// Subject variables (<c>{subject.user.id}</c>, …) in compiled filters are substituted per request
/// (<see cref="AbacSubjectVariableResolver.SubstituteFilterJson"/>). Action-level and filter results are cached in
/// an L1 <see cref="IMemoryCache"/> (key: org + set id + action + subject hash, TTL <c>Abac:L1:Ttl</c>).
/// </para>
/// </summary>
public class AbacEvaluator : IAbacEvaluator
{
    private const string ModeAuthorize = "authorize";
    private const string ModeResource = "resource";
    private const string ModeList = "list";
    private const string ModeSearch = "search";

    private readonly IAbacPolicyReader _policyReader;
    private readonly IAbacSubjectFactory _subjectFactory;
    private readonly ILogger<AbacEvaluator> _logger;
    private readonly IMemoryCache? _memoryCache;
    private readonly AbacRuntimeOptions _options;
    private readonly AbacRuntimeMetrics _metrics;

    /// <summary>Initializes a new instance of <see cref="AbacEvaluator"/>.</summary>
    public AbacEvaluator(
        IAbacPolicyReader policyReader,
        IAbacSubjectFactory subjectFactory,
        ILogger<AbacEvaluator> logger,
        IMemoryCache? memoryCache = null,
        IOptions<AbacRuntimeOptions>? options = null,
        AbacRuntimeMetrics? metrics = null)
    {
        _policyReader = policyReader;
        _subjectFactory = subjectFactory;
        _logger = logger;
        _memoryCache = memoryCache;
        _options = options?.Value ?? new AbacRuntimeOptions();
        _metrics = metrics ?? AbacRuntimeMetrics.Shared;
    }

    /// <inheritdoc/>
    public Task<AbacDecisionResult> AuthorizeAsync(Guid organizationId, string action, CancellationToken ct = default)
        => RunAsync(ModeAuthorize, organizationId, action, cacheable: true, EvaluateActionLevel, ct);

    /// <inheritdoc/>
    public Task<AbacDecisionResult> AuthorizeAsync(Guid organizationId, string action, IReadOnlyDictionary<string, object?> resource, CancellationToken ct = default)
        => RunAsync(ModeResource, organizationId, action, cacheable: false, (s, p, a) => EvaluateResource(s, p, a, resource), ct);

    /// <inheritdoc/>
    public Task<AbacDecisionResult> BuildListFilterAsync(Guid organizationId, string action, CancellationToken ct = default)
        => RunAsync(ModeList, organizationId, action, cacheable: true, EvaluateListFilter, ct);

    /// <inheritdoc/>
    public Task<AbacDecisionResult> BuildSearchFilterAsync(Guid organizationId, string action, CancellationToken ct = default)
        => RunAsync(ModeSearch, organizationId, action, cacheable: true, EvaluateSearchFilter, ct);

    /// <inheritdoc/>
    /// <remarks>
    /// Subject-independent: <c>true</c> when any deny rule's action patterns match <paramref name="action"/>, when no
    /// rule set is available, on an engine-version mismatch, or for a confirmed-empty set without rollout fallback.
    /// </remarks>
    public async Task<bool> HasDenyRulesForActionAsync(Guid organizationId, string action, CancellationToken ct = default)
    {
        var snapshot = await _policyReader.GetSnapshotAsync(organizationId, ct);
        if (snapshot == null || snapshot.Status == AbacPolicySnapshotStatus.Unavailable)
            return true;

        if (snapshot.Status == AbacPolicySnapshotStatus.ConfirmedEmpty)
            return !snapshot.AllowAllWhenEmpty;

        if (snapshot.Policies.Any(p => p.CompilerVersion != AbacEngineVersion.Current))
            return true;

        return snapshot.Policies
            .SelectMany(p => p.CompiledRules)
            .Any(r => r.Effect == AbacEffect.Deny && ActionMatches(r.ActionPatterns, action));
    }

    // ── Pipeline ──────────────────────────────────────────────────────────────

    private async Task<AbacDecisionResult> RunAsync(
        string mode,
        Guid organizationId,
        string action,
        bool cacheable,
        Func<AbacSubject, IReadOnlyList<CompiledPolicy>, string, Evaluation> evaluate,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheHit = false;
        Evaluation evaluation;

        try
        {
            var subject = _subjectFactory.BuildSubject();
            if (subject == null)
            {
                _logger.LogWarning("ABAC: No authenticated subject for action '{Action}' in org '{OrgId}' — denying.", action, organizationId);
                evaluation = Evaluation.Deny("no_subject");
            }
            else
            {
                var snapshot = await _policyReader.GetSnapshotAsync(organizationId, ct);
                var gate = ApplyGates(snapshot, organizationId);
                if (gate != null)
                {
                    evaluation = gate.Value;
                }
                else
                {
                    var cacheKey = cacheable ? BuildCacheKey(mode, organizationId, snapshot!.SetId, action, subject) : null;
                    if (cacheKey != null && _memoryCache!.TryGetValue(cacheKey, out Evaluation cached))
                    {
                        cacheHit = true;
                        evaluation = cached;
                    }
                    else
                    {
                        evaluation = evaluate(subject, snapshot!.Policies, action);
                        if (cacheKey != null)
                            _memoryCache!.Set(cacheKey, evaluation, _options.L1.Ttl);
                    }
                }
            }
        }
        finally
        {
            stopwatch.Stop();
        }

        _metrics.RecordEvaluationDuration(mode, stopwatch.Elapsed.TotalMilliseconds, cacheHit);
        _metrics.RecordDecision(mode, evaluation.Decision.ToString().ToLowerInvariant(), evaluation.Reason);

        return evaluation.ToResult();
    }

    private Evaluation? ApplyGates(AbacPolicySnapshot? snapshot, Guid organizationId)
    {
        if (snapshot == null || snapshot.Status == AbacPolicySnapshotStatus.Unavailable)
        {
            _logger.LogWarning("ABAC: Rule set unavailable for org '{OrgId}' — denying.", organizationId);
            return Evaluation.Deny("unavailable");
        }

        if (snapshot.Status == AbacPolicySnapshotStatus.ConfirmedEmpty)
        {
            if (snapshot.AllowAllWhenEmpty)
                return Evaluation.Allow(null, null, "rollout_allow_all");

            _logger.LogDebug("ABAC: Org '{OrgId}' has no policies and no rollout fallback — implicit deny.", organizationId);
            return Evaluation.Deny("empty");
        }

        var mismatch = snapshot.Policies.FirstOrDefault(p => p.CompilerVersion != AbacEngineVersion.Current);
        if (mismatch != null)
        {
            _logger.LogError(
                "ABAC: Engine version mismatch — policy '{PolicyId}' compiler version '{PolicyVer}', reader expects '{ReaderVer}'. Denying.",
                mismatch.PolicyId, mismatch.CompilerVersion, AbacEngineVersion.Current);
            return new Evaluation(AbacDecision.Deny, null, null, true, "version_mismatch");
        }

        return null;
    }

    // ── Modes ─────────────────────────────────────────────────────────────────

    private Evaluation EvaluateActionLevel(AbacSubject subject, IReadOnlyList<CompiledPolicy> policies, string action)
    {
        var hasAllow = false;
        foreach (var rule in MatchingRules(policies, action, subject))
        {
            if (rule.Effect == AbacEffect.Deny)
            {
                if (!rule.Metadata.HasResourceConditions)
                    return Evaluation.Deny("deny_rule");
                continue;
            }

            hasAllow = true;
        }

        return hasAllow ? Evaluation.Allow(null, null, "allow_rule") : Evaluation.Deny("no_allow");
    }

    private Evaluation EvaluateResource(AbacSubject subject, IReadOnlyList<CompiledPolicy> policies, string action, IReadOnlyDictionary<string, object?> resource)
    {
        var hasAllow = false;
        foreach (var rule in MatchingRules(policies, action, subject))
        {
            var resourceMatches = ResourceMatches(rule, resource, subject);
            if (rule.Effect == AbacEffect.Deny)
            {
                if (resourceMatches)
                    return Evaluation.Deny("deny_rule");
                continue;
            }

            if (resourceMatches)
                hasAllow = true;
        }

        return hasAllow ? Evaluation.Allow(null, null, "allow_rule") : Evaluation.Deny("no_allow");
    }

    private Evaluation EvaluateListFilter(AbacSubject subject, IReadOnlyList<CompiledPolicy> policies, string action)
    {
        var hasAllow = false;
        var allowAll = false;
        var allowFilters = new List<string>();
        var denyFilters = new List<string>();

        foreach (var rule in MatchingRules(policies, action, subject))
        {
            if (rule.Effect == AbacEffect.Deny)
            {
                if (!rule.Metadata.HasResourceConditions)
                    return Evaluation.Deny("deny_rule");

                var denyFilter = Substitute(rule.MongoDbFilterJson, subject, AbacFilterDialect.MongoDb, rule.RuleId);
                if (denyFilter == null)
                {
                    _logger.LogWarning(
                        "ABAC: Deny rule '{RuleId}' has resource conditions not expressible as a MongoDB filter for action '{Action}' — failing closed.",
                        rule.RuleId, action);
                    return Evaluation.Deny("deny_unexpressible");
                }

                denyFilters.Add(denyFilter);
                continue;
            }

            hasAllow = true;
            if (!rule.Metadata.HasResourceConditions)
            {
                allowAll = true;
                continue;
            }

            var allowFilter = Substitute(rule.MongoDbFilterJson, subject, AbacFilterDialect.MongoDb, rule.RuleId);
            if (allowFilter == null)
            {
                _logger.LogWarning(
                    "ABAC: Allow rule '{RuleId}' has resource conditions not expressible as a MongoDB filter for action '{Action}' — ignored.",
                    rule.RuleId, action);
                continue;
            }

            allowFilters.Add(allowFilter);
        }

        if (!hasAllow)
            return Evaluation.Deny("no_allow");

        string? allowClause = null;
        if (!allowAll)
        {
            if (allowFilters.Count == 0)
                return Evaluation.Deny("allow_unexpressible");

            allowClause = allowFilters.Count == 1 ? allowFilters[0] : $"{{\"$or\":[{string.Join(",", allowFilters)}]}}";
        }

        if (denyFilters.Count == 0)
            return Evaluation.Allow(allowClause, null, "allow_rule");

        var norClause = $"{{\"$nor\":[{string.Join(",", denyFilters)}]}}";
        var filter = allowClause == null ? norClause : $"{{\"$and\":[{allowClause},{norClause}]}}";
        return Evaluation.Allow(filter, null, "allow_rule");
    }

    /// <summary>
    /// Builds an Elasticsearch filter using deny-first semantics (§18.4): allow conditions form the
    /// <c>filter</c>/<c>should</c> group, deny conditions the <c>must_not</c> group. Rules not expressible in
    /// Elasticsearch fail closed.
    /// </summary>
    private Evaluation EvaluateSearchFilter(AbacSubject subject, IReadOnlyList<CompiledPolicy> policies, string action)
    {
        var hasAllow = false;
        var allowAll = false;
        var allowFilters = new List<string>();
        var denyFilters = new List<string>();

        foreach (var rule in MatchingRules(policies, action, subject))
        {
            if (rule.Effect == AbacEffect.Deny)
            {
                if (!rule.Metadata.HasResourceConditions)
                    return Evaluation.Deny("deny_rule");

                var denyFilter = Substitute(rule.SearchFilter, subject, AbacFilterDialect.Elasticsearch, rule.RuleId);
                if (denyFilter == null)
                {
                    _logger.LogWarning(
                        "ABAC: Deny rule '{RuleId}' has resource conditions not expressible as an Elasticsearch filter for action '{Action}' — failing closed.",
                        rule.RuleId, action);
                    return Evaluation.Deny("deny_unexpressible");
                }

                denyFilters.Add(denyFilter);
                continue;
            }

            hasAllow = true;
            if (!rule.Metadata.HasResourceConditions)
            {
                allowAll = true;
                continue;
            }

            var allowFilter = Substitute(rule.SearchFilter, subject, AbacFilterDialect.Elasticsearch, rule.RuleId);
            if (allowFilter == null)
            {
                _logger.LogWarning(
                    "ABAC: Allow rule '{RuleId}' has resource conditions not expressible as an Elasticsearch filter for action '{Action}' — ignored.",
                    rule.RuleId, action);
                continue;
            }

            allowFilters.Add(allowFilter);
        }

        if (!hasAllow)
            return Evaluation.Deny("no_allow");

        string? allowClause = null;
        if (!allowAll)
        {
            if (allowFilters.Count == 0)
            {
                _logger.LogWarning(
                    "ABAC: Allow matched for action '{Action}' but no allow rule was expressible as an Elasticsearch filter — failing closed.",
                    action);
                return Evaluation.Deny("allow_unexpressible");
            }

            allowClause = allowFilters.Count == 1
                ? allowFilters[0]
                : $"{{\"bool\":{{\"should\":[{string.Join(",", allowFilters)}],\"minimum_should_match\":1}}}}";
        }

        if (allowAll && denyFilters.Count == 0)
            return Evaluation.Allow(null, null, "allow_rule");

        var parts = new List<string>();
        if (allowClause != null)
            parts.Add($"\"filter\":[{allowClause}]");
        if (denyFilters.Count > 0)
            parts.Add($"\"must_not\":[{string.Join(",", denyFilters)}]");

        return Evaluation.Allow(null, $"{{\"bool\":{{{string.Join(",", parts)}}}}}", "allow_rule");
    }

    // ── Rule matching ─────────────────────────────────────────────────────────

    private IEnumerable<CompiledRule> MatchingRules(IReadOnlyList<CompiledPolicy> policies, string action, AbacSubject subject)
    {
        foreach (var policy in policies)
        {
            foreach (var rule in policy.CompiledRules)
            {
                if (ActionMatches(rule.ActionPatterns, action) && SubjectMatches(rule, subject))
                    yield return rule;
            }
        }
    }

    /// <summary>
    /// Evaluates a rule's subject conditions. An unparseable condition fails closed: a deny rule is treated
    /// as matching, an allow rule as not matching.
    /// </summary>
    private bool SubjectMatches(CompiledRule rule, AbacSubject subject)
    {
        if (!rule.Metadata.HasSubjectConditions)
            return true;

        try
        {
            rule.SubjectConditions.CompiledAst ??= ConditionNodeParser.Parse(rule.SubjectConditions.RawJson);
            return AbacConditionEvaluator.Evaluate(rule.SubjectConditions.CompiledAst, subject);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException or InvalidCastException)
        {
            _logger.LogError(ex, "ABAC: Subject conditions of rule '{RuleId}' cannot be evaluated — failing closed.", rule.RuleId);
            return rule.Effect == AbacEffect.Deny;
        }
    }

    /// <summary>
    /// Evaluates a rule's resource conditions in-process. Missing or unparseable conditions on a rule flagged with
    /// resource conditions fail closed: a deny rule matches, an allow rule does not.
    /// </summary>
    private bool ResourceMatches(CompiledRule rule, IReadOnlyDictionary<string, object?> resource, AbacSubject subject)
    {
        if (!rule.Metadata.HasResourceConditions)
            return true;

        try
        {
            rule.ResourceConditions.CompiledAst ??= ConditionNodeParser.Parse(rule.ResourceConditions.RawJson);
            if (rule.ResourceConditions.CompiledAst == null)
            {
                _logger.LogWarning("ABAC: Rule '{RuleId}' is flagged with resource conditions but has none — failing closed.", rule.RuleId);
                return rule.Effect == AbacEffect.Deny;
            }

            return AbacResourceConditionEvaluator.Evaluate(rule.ResourceConditions.CompiledAst, resource, subject);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException or InvalidCastException)
        {
            _logger.LogError(ex, "ABAC: Resource conditions of rule '{RuleId}' cannot be evaluated — failing closed.", rule.RuleId);
            return rule.Effect == AbacEffect.Deny;
        }
    }

    /// <summary>Substitutes subject variables; returns <c>null</c> for missing or invalid filter JSON.</summary>
    private string? Substitute(string? json, AbacSubject subject, AbacFilterDialect dialect, string ruleId)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return AbacSubjectVariableResolver.SubstituteFilterJson(json, subject, dialect);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "ABAC: Filter of rule '{RuleId}' is not valid JSON — treated as unexpressible.", ruleId);
            return null;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="action"/> matches any of the <paramref name="patterns"/>.
    /// Patterns ending with <c>*</c> are prefix-wildcards; all others are exact (case-insensitive).
    /// </summary>
    internal static bool ActionMatches(string[] patterns, string action)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.EndsWith('*'))
            {
                if (action.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (string.Equals(pattern, action, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ── L1 cache ──────────────────────────────────────────────────────────────

    private string? BuildCacheKey(string mode, Guid organizationId, string? setId, string action, AbacSubject subject)
    {
        if (_memoryCache == null || !_options.L1.Enabled || _options.L1.Ttl <= TimeSpan.Zero || string.IsNullOrEmpty(setId))
            return null;

        return $"abac:l1:{mode}:{organizationId:N}:{setId}:{action.ToLowerInvariant()}:{ComputeSubjectHash(subject)}";
    }

    /// <summary>Stable hash over all subject attributes that can influence a decision.</summary>
    internal static string ComputeSubjectHash(AbacSubject subject)
    {
        var builder = new StringBuilder();
        builder.Append(subject.User.Id.ToString("N")).Append('|')
            .Append(subject.User.Email.ToLowerInvariant()).Append('|');

        foreach (var role in subject.Roles.Select(r => r.ToLowerInvariant()).Order(StringComparer.Ordinal))
            builder.Append(role).Append(',');

        builder.Append('|');
        foreach (var team in subject.Teams.Select(t => $"{t.Id:N}:{t.Name.ToLowerInvariant()}").Order(StringComparer.Ordinal))
            builder.Append(team).Append(',');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>Immutable evaluation result (safe to share via the L1 cache).</summary>
    private readonly record struct Evaluation(AbacDecision Decision, string? MongoDbFilterJson, string? SearchFilterJson, bool IsVersionMismatch, string Reason)
    {
        public static Evaluation Deny(string reason) => new(AbacDecision.Deny, null, null, false, reason);

        public static Evaluation Allow(string? mongoFilter, string? searchFilter, string reason) => new(AbacDecision.Allow, mongoFilter, searchFilter, false, reason);

        public AbacDecisionResult ToResult() => new()
        {
            Decision = Decision,
            MongoDbFilterJson = MongoDbFilterJson,
            SearchFilterJson = SearchFilterJson,
            IsVersionMismatch = IsVersionMismatch
        };
    }
}

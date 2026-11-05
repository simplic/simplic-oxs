namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Runtime ABAC evaluation engine (deny-first, zero-trust).
/// </summary>
public interface IAbacEvaluator
{
    /// <summary>
    /// Action-level check without a concrete resource: is the current subject allowed to perform
    /// <paramref name="action"/> at all? Resource conditions are not evaluated; use
    /// <see cref="BuildListFilterAsync"/> for list reads or the resource overload for single objects.
    /// </summary>
    /// <remarks>
    /// <b>Not sufficient on its own to return resource data.</b> Conditional deny rules (deny rules that
    /// carry resource conditions) are intentionally ignored here: an Allow result only means the action
    /// <i>could potentially</i> be allowed for some resource — it does not imply that any authorized resource
    /// exists, and it does not authorize a <i>specific</i> resource. Using it as the
    /// sole gate for a data-returning path can leak rows that a conditional deny rule would hide. It MUST
    /// always be paired with the data-level check — the resource overload
    /// (<see cref="AuthorizeAsync(Guid, string, IReadOnlyDictionary{string, object?}, CancellationToken)"/>)
    /// for a single object, or <see cref="BuildListFilterAsync"/> / <see cref="BuildSearchFilterAsync"/> for
    /// read-many. Standalone use is limited to coarse endpoint-reachability gating (see ABAC concept §7.4).
    /// </remarks>
    Task<AbacDecisionResult> AuthorizeAsync(Guid organizationId, string action, CancellationToken ct = default);

    /// <summary>
    /// Authorises <paramref name="action"/> on a concrete resource. Resource conditions of
    /// matching Allow and Deny rules are evaluated in-process against <paramref name="resource"/>
    /// (keys = policy field names, dot-notation supported for nested dictionaries).
    /// </summary>
    Task<AbacDecisionResult> AuthorizeAsync(Guid organizationId, string action, IReadOnlyDictionary<string, object?> resource, CancellationToken ct = default);

    /// <summary>Builds the MongoDB egress filter for list/single reads.</summary>
    Task<AbacDecisionResult> BuildListFilterAsync(Guid organizationId, string action, CancellationToken ct = default);

    /// <summary>Builds the Elasticsearch filter for search reads.</summary>
    Task<AbacDecisionResult> BuildSearchFilterAsync(Guid organizationId, string action, CancellationToken ct = default);

    /// <summary>
    /// Organisation-level check used by message-bus consumers (§7.6): returns <c>true</c> when the
    /// organisation's active set contains any Deny rule matching <paramref name="action"/>, or when
    /// the set is unavailable (fail closed). Independent of the current subject.
    /// </summary>
    Task<bool> HasDenyRulesForActionAsync(Guid organizationId, string action, CancellationToken ct = default);
}
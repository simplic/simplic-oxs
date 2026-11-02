using Simplic.OxS.ABAC.Policy;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// The outcome of an ABAC authorization evaluation.
/// Carries the decision together with any pre-built MongoDB filter
/// needed for list/read-many flows.
/// </summary>
public class AbacDecisionResult
{
    /// <summary>The authorization decision (Allow or Deny).</summary>
    public AbacDecision Decision { get; set; }

    /// <summary>
    /// Combined MongoDB filter JSON for list/read-many queries.
    /// <para>
    /// Null when <see cref="Decision"/> is <see cref="AbacDecision.Allow"/> and
    /// at least one matching Allow rule has no resource conditions —
    /// meaning the subject has unrestricted access and no extra filter is needed.
    /// </para>
    /// <para>
    /// Non-null when <see cref="Decision"/> is <see cref="AbacDecision.Allow"/> and
    /// every matching Allow rule carries resource conditions; callers should
    /// AND this document into their MongoDB query.
    /// </para>
    /// <para>Ignored when <see cref="Decision"/> is <see cref="AbacDecision.Deny"/>.</para>
    /// </summary>
    public string? MongoDbFilterJson { get; set; }

    /// <summary>
    /// Combined Elasticsearch Query DSL filter (a <c>bool</c> clause) for search / read-many flows.
    /// <para>
    /// Null when <see cref="Decision"/> is <see cref="AbacDecision.Allow"/> and at least one
    /// matching Allow rule has no resource conditions, and no Deny rule contributes a
    /// <c>must_not</c> — meaning the subject has unrestricted access and no extra filter is needed.
    /// </para>
    /// <para>
    /// Non-null when the subject's access is restricted; callers should AND this clause into
    /// their Elasticsearch query (see <see cref="Simplic.OxS.ABAC.Search.AbacSearchQuery.Apply"/>).
    /// </para>
    /// <para>Ignored when <see cref="Decision"/> is <see cref="AbacDecision.Deny"/>.</para>
    /// </summary>
    public string? SearchFilterJson { get; set; }

    /// <summary>
    /// <c>true</c> when the denial was caused by a compiler-version / engine-version mismatch.
    /// Callers should treat this as a high-priority alert.
    /// </summary>
    public bool IsVersionMismatch { get; set; }
}

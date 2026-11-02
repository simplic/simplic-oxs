namespace Simplic.OxS.ABAC.Search;

/// <summary>
/// Helper for composing a user's Elasticsearch query with the ABAC-generated
/// security filter. Client-agnostic — operates on raw ES Query DSL JSON strings so
/// it works with any Elasticsearch/OpenSearch client the search service happens to use.
/// </summary>
public static class AbacSearchQuery
{
    /// <summary>
    /// ANDs the ABAC security filter into a user query by wrapping both in a
    /// <c>bool</c> query (<paramref name="userQueryJson"/> in <c>must</c>, the ABAC
    /// clause in <c>filter</c>).
    /// <para>
    /// When <paramref name="abacSearchFilterJson"/> is null or whitespace, the subject
    /// has unrestricted access and <paramref name="userQueryJson"/> is returned unchanged.
    /// </para>
    /// <para>
    /// Note: organisation/tenant scoping is the search service's responsibility (as with
    /// the MongoDB base filter) — this method only applies the policy-derived restriction.
    /// </para>
    /// </summary>
    /// <param name="userQueryJson">The user's query as an ES Query DSL JSON object.</param>
    /// <param name="abacSearchFilterJson">
    /// The ABAC filter from <see cref="Simplic.OxS.ABAC.Runtime.AbacDecisionResult.SearchFilterJson"/>.
    /// </param>
    /// <returns>The combined ES Query DSL JSON object.</returns>
    public static string Apply(string userQueryJson, string? abacSearchFilterJson)
    {
        if (string.IsNullOrWhiteSpace(abacSearchFilterJson))
            return userQueryJson;

        return $"{{\"bool\":{{\"must\":[{userQueryJson}],\"filter\":[{abacSearchFilterJson}]}}}}";
    }
}

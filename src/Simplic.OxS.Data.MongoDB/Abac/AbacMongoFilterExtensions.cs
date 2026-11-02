using MongoDB.Bson;
using MongoDB.Driver;

namespace Simplic.OxS.Data.MongoDB.Abac;

/// <summary>
/// Extension methods for applying ABAC-generated MongoDB filter documents to queries.
/// </summary>
public static class AbacMongoFilterExtensions
{
    /// <summary>
    /// ANDs an ABAC-generated MongoDB filter JSON string into an existing
    /// <see cref="FilterDefinition{TDocument}"/>.
    /// <para>
    /// Typical usage in a list endpoint:
    /// <code>
    /// var result = await _evaluator.BuildListFilterAsync(orgId, "oxs:document:list", ct);
    /// if (result.Decision == AbacDecision.Deny)
    ///     return Forbid();
    ///
    /// var filter = Builders&lt;MyDoc&gt;.Filter.Eq(d => d.IsDeleted, false)
    ///                                        .ApplyAbacFilter(result.MongoDbFilterJson);
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="baseFilter">The base query filter to extend.</param>
    /// <param name="abacFilterJson">
    /// MongoDB filter JSON from <see cref="Simplic.OxS.ABAC.Runtime.AbacDecisionResult.MongoDbFilterJson"/>.
    /// When null or whitespace, <paramref name="baseFilter"/> is returned unchanged
    /// (subject has unrestricted access — no extra predicate needed).
    /// </param>
    /// <returns>The combined filter definition.</returns>
    public static FilterDefinition<TDocument> ApplyAbacFilter<TDocument>(
        this FilterDefinition<TDocument> baseFilter,
        string? abacFilterJson)
    {
        if (string.IsNullOrWhiteSpace(abacFilterJson))
            return baseFilter;

        var abacDoc = BsonDocument.Parse(abacFilterJson);
        var abacFilter = new BsonDocumentFilterDefinition<TDocument>(abacDoc);

        return Builders<TDocument>.Filter.And(baseFilter, abacFilter);
    }
}

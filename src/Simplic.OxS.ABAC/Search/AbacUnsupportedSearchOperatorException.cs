namespace Simplic.OxS.ABAC.Search;

/// <summary>
/// Thrown when an ABAC DSL condition cannot be translated into an Elasticsearch
/// query filter (for example the <c>$contains</c> / <c>$ncontains</c> operators,
/// which have no cheap exact-match equivalent on a keyword field).
/// <para>
/// Gating is driven conceptually by
/// <see cref="Simplic.OxS.ABAC.Contract.AbacOperatorCapabilities.SupportsSearch"/>:
/// when an operator is not search-supported, the policy compiler leaves the rule's
/// <see cref="Simplic.OxS.ABAC.Compiled.CompiledRule.SearchFilter"/> unset and the
/// runtime fails closed rather than returning unauthorised hits.
/// </para>
/// </summary>
public class AbacUnsupportedSearchOperatorException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="AbacUnsupportedSearchOperatorException"/>.
    /// </summary>
    public AbacUnsupportedSearchOperatorException(string message) : base(message)
    {
    }
}

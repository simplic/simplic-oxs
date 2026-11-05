using System.Collections;
using System.Text.Json;
using Simplic.OxS.ABAC.Ast;
using Simplic.OxS.ABAC.Contract;

namespace Simplic.OxS.ABAC.Search;

/// <summary>
/// Translates a <see cref="ConditionNode"/> AST (resource conditions) into an
/// Elasticsearch Query DSL filter clause, serialised as a JSON string.
/// <para>
/// The produced fragment is a single ES query clause (<c>term</c>, <c>terms</c>,
/// <c>range</c> or a <c>bool</c> combinator) that callers drop into a <c>filter</c>
/// or <c>must_not</c> context. Operator mapping follows §18.3 of the ABAC concept:
/// <list type="bullet">
///   <item><c>$eq</c> → <c>term</c></item>
///   <item><c>$ne</c> → <c>bool.must_not</c> + <c>term</c></item>
///   <item><c>$in</c> → <c>terms</c></item>
///   <item><c>$nin</c> → <c>bool.must_not</c> + <c>terms</c></item>
///   <item><c>$gt</c> / <c>$gte</c> / <c>$lt</c> / <c>$lte</c> → <c>range</c></item>
///   <item><c>$and</c> → <c>bool.filter</c>; <c>$or</c> → <c>bool.should</c> (minimum_should_match 1)</item>
///   <item><c>$contains</c> / <c>$ncontains</c> → not translatable (gated, throws)</item>
/// </list>
/// </para>
/// <para>
/// Subject conditions are never translated here — they are evaluated in-process
/// against the current subject. Only resource conditions become search filters.
/// </para>
/// </summary>
public static class AbacSearchFilterBuilder
{
    // Default options: dictionary keys (ES operators and field paths) are written verbatim.
    private static readonly JsonSerializerOptions JsonOptions = new();

    /// <summary>
    /// Builds an Elasticsearch filter clause JSON string from a resource-condition AST.
    /// Returns <c>null</c> when <paramref name="node"/> is <c>null</c> (no restriction).
    /// </summary>
    /// <param name="node">The resource-condition AST to translate.</param>
    /// <param name="fieldMappings">
    /// Optional policy-field → backend mapping (from <see cref="IAbacCompileContract.FieldMappings"/>).
    /// When provided, each condition field is resolved to its <see cref="AbacFieldMapping.SearchPath"/>
    /// (and optional <see cref="AbacFieldMapping.SearchValuePrefix"/>) and the builder <b>fails closed</b>:
    /// fields without a mapping, or with no <c>SearchPath</c>, throw. When <c>null</c> the raw policy
    /// field names are emitted verbatim (standalone DSL translation; used by unit tests).
    /// </param>
    /// <exception cref="AbacUnsupportedSearchOperatorException">
    /// Thrown when the AST contains an operator that has no Elasticsearch equivalent, or (when
    /// <paramref name="fieldMappings"/> is provided) a field that is not searchable.
    /// </exception>
    public static string? Build(ConditionNode? node, IReadOnlyDictionary<string, AbacFieldMapping>? fieldMappings = null)
    {
        if (node == null)
            return null;

        var clause = BuildNode(node, fieldMappings);
        return JsonSerializer.Serialize(clause, JsonOptions);
    }

    private static object BuildNode(ConditionNode node, IReadOnlyDictionary<string, AbacFieldMapping>? fieldMappings)
        => node.NodeType switch
        {
            ConditionNodeType.And => BoolClause("filter", node.Children, fieldMappings),
            ConditionNodeType.Or => BoolClause("should", node.Children, fieldMappings, minimumShouldMatch: 1),
            ConditionNodeType.Comparison => BuildComparison(node, fieldMappings),
            _ => throw new AbacUnsupportedSearchOperatorException($"Unsupported condition node type '{node.NodeType}'.")
        };

    private static Dictionary<string, object?> BoolClause(string occur, IList<ConditionNode> children, IReadOnlyDictionary<string, AbacFieldMapping>? fieldMappings, int? minimumShouldMatch = null)
    {
        var clauses = children.Select(child => BuildNode(child, fieldMappings)).ToList();
        var boolBody = new Dictionary<string, object?> { [occur] = clauses };

        if (minimumShouldMatch.HasValue)
            boolBody["minimum_should_match"] = minimumShouldMatch.Value;

        return new Dictionary<string, object?> { ["bool"] = boolBody };
    }

    private static Dictionary<string, object?> BuildComparison(ConditionNode node, IReadOnlyDictionary<string, AbacFieldMapping>? fieldMappings)
    {
        if (string.IsNullOrEmpty(node.Field) || string.IsNullOrEmpty(node.Operator))
            throw new AbacUnsupportedSearchOperatorException("Comparison node is missing a field or operator.");

        var (field, valuePrefix) = ResolveField(node.Field!, node.Operator!, fieldMappings);
        var value = ApplyValuePrefix(node.Value, valuePrefix);

        return node.Operator switch
        {
            "$eq" => Term(field, value),
            "$ne" => MustNot(Term(field, value)),
            "$in" => Terms(field, value),
            "$nin" => MustNot(Terms(field, value)),
            "$gt" => Range(field, "gt", value),
            "$gte" => Range(field, "gte", value),
            "$lt" => Range(field, "lt", value),
            "$lte" => Range(field, "lte", value),
            "$contains" or "$ncontains" => throw new AbacUnsupportedSearchOperatorException(
                $"Operator '{node.Operator}' on field '{node.Field}' cannot be translated to an Elasticsearch filter."),
            _ => throw new AbacUnsupportedSearchOperatorException(
                $"Unknown operator '{node.Operator}' on field '{node.Field}'.")
        };
    }

    /// <summary>
    /// Resolves a policy field name to its Elasticsearch path and optional principal value prefix.
    /// Fails closed when mappings are provided but the field is unmapped, not searchable, or a range
    /// operator targets a principal-style field.
    /// </summary>
    private static (string path, string? valuePrefix) ResolveField(string policyField, string op, IReadOnlyDictionary<string, AbacFieldMapping>? fieldMappings)
    {
        // No mapping context: standalone DSL translation emits raw field names verbatim.
        if (fieldMappings == null)
            return (policyField, null);

        if (!fieldMappings.TryGetValue(policyField, out var mapping))
            throw new AbacUnsupportedSearchOperatorException(
                $"Field '{policyField}' has no field mapping; cannot translate to a search filter.");

        if (string.IsNullOrEmpty(mapping.SearchPath))
            throw new AbacUnsupportedSearchOperatorException(
                $"Field '{policyField}' is not searchable (no SearchPath in its field mapping).");

        if (!string.IsNullOrEmpty(mapping.SearchValuePrefix) && IsRangeOperator(op))
            throw new AbacUnsupportedSearchOperatorException(
                $"Range operator '{op}' cannot be applied to principal-style field '{policyField}'.");

        return (mapping.SearchPath!, mapping.SearchValuePrefix);
    }

    private static bool IsRangeOperator(string op)
        => op is "$gt" or "$gte" or "$lt" or "$lte";

    /// <summary>
    /// Prefixes principal-style values as <c>{prefix}:{value}</c> (scalars and array elements),
    /// mirroring index-time materialization. Returns the value unchanged when no prefix applies.
    /// </summary>
    private static object? ApplyValuePrefix(object? value, string? valuePrefix)
    {
        if (string.IsNullOrEmpty(valuePrefix) || value == null)
            return value;

        if (value is IEnumerable enumerable and not string)
        {
            var prefixed = new List<object?>();
            foreach (var item in enumerable)
                prefixed.Add($"{valuePrefix}:{item}");
            return prefixed;
        }

        return $"{valuePrefix}:{value}";
    }

    private static Dictionary<string, object?> Term(string field, object? value)
        => new() { ["term"] = new Dictionary<string, object?> { [field] = value } };

    private static Dictionary<string, object?> Terms(string field, object? value)
        => new() { ["terms"] = new Dictionary<string, object?> { [field] = value } };

    private static Dictionary<string, object?> Range(string field, string op, object? value)
        => new() { ["range"] = new Dictionary<string, object?> { [field] = new Dictionary<string, object?> { [op] = value } } };

    private static Dictionary<string, object?> MustNot(object clause)
        => new() { ["bool"] = new Dictionary<string, object?> { ["must_not"] = new List<object> { clause } } };
}

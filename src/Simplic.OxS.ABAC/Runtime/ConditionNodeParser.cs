using System.Text.Json;
using Simplic.OxS.ABAC.Ast;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Parses the JSON-first condition DSL stored in <see cref="Simplic.OxS.ABAC.Compiled.RuleConditions.RawJson"/>
/// into a typed <see cref="ConditionNode"/> AST for in-process evaluation.
/// <para>
/// The DSL mirrors MongoDB query syntax: top-level keys are implicitly ANDed;
/// <c>$and</c> / <c>$or</c> introduce explicit combinators.
/// Field comparisons follow the shape <c>{ "fieldPath": { "$op": value } }</c>.
/// </para>
/// </summary>
public static class ConditionNodeParser
{
    /// <summary>
    /// Parses a raw-JSON condition dictionary into a ConditionNode AST.
    /// Returns <c>null</c> when <paramref name="rawJson"/> is null or empty
    /// (which the evaluator treats as "always true" — no conditions).
    /// </summary>
    public static ConditionNode? Parse(IDictionary<string, object>? rawJson)
    {
        if (rawJson == null || rawJson.Count == 0)
            return null;

        var children = new List<ConditionNode>(rawJson.Count);

        foreach (var (key, value) in rawJson)
        {
            if (key == "$and")
                children.Add(ParseLogical(ConditionNodeType.And, value));
            else if (key == "$or")
                children.Add(ParseLogical(ConditionNodeType.Or, value));
            else
                children.Add(ParseFieldComparison(key, value));
        }

        return children.Count == 1
            ? children[0]
            : new ConditionNode { NodeType = ConditionNodeType.And, Children = children };
    }

    private static ConditionNode ParseLogical(ConditionNodeType type, object value)
    {
        var items = ExtractList(value);
        var children = items
            .Select(item => Parse(ExtractDictionary(item)))
            .OfType<ConditionNode>()
            .ToList();

        return new ConditionNode { NodeType = type, Children = children };
    }

    private static ConditionNode ParseFieldComparison(string field, object value)
    {
        var dict = ExtractDictionary(value)
            ?? throw new InvalidOperationException(
                $"ABAC condition for field '{field}' must be an object (e.g. {{\"$eq\": \"value\"}}).");

        if (dict.Count == 0)
            throw new InvalidOperationException($"ABAC condition for field '{field}' has no operator.");

        var (op, opValue) = dict.First();

        return new ConditionNode
        {
            NodeType = ConditionNodeType.Comparison,
            Field = field,
            Operator = op,
            Value = NormalizeValue(opValue)
        };
    }

    // ── JSON type helpers ─────────────────────────────────────────────────────

    /// <summary>Extracts a list from a <see cref="JsonElement"/> (Array) or a plain <see cref="IList{T}"/>.</summary>
    private static IList<object> ExtractList(object value)
    {
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
            return je.EnumerateArray().Select(e => (object)e).ToList();

        if (value is IList<object> list)
            return list;

        if (value is System.Collections.IList rawList)
            return rawList.Cast<object>().ToList();

        return [];
    }

    /// <summary>Extracts a string-keyed dictionary from a <see cref="JsonElement"/> (Object) or an <see cref="IDictionary{TKey,TValue}"/>.</summary>
    private static IDictionary<string, object>? ExtractDictionary(object? value)
    {
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Object)
            return je.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value);

        if (value is IDictionary<string, object> dict)
            return dict;

        return null;
    }

    /// <summary>Normalizes a <see cref="JsonElement"/> to a plain CLR type; returns non-JsonElement values unchanged.</summary>
    private static object? NormalizeValue(object? value)
    {
        if (value is not JsonElement je)
            return value;

        return je.ValueKind switch
        {
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number when je.TryGetInt64(out var l) => l,
            JsonValueKind.Number => je.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => je.EnumerateArray()
                                     .Select(e => NormalizeValue(e))
                                     .ToList(),
            _ => je.ToString()
        };
    }
}

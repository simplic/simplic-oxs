using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Simplic.OxS.ABAC.Subject;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Target dialect of a pre-compiled filter JSON that contains <c>{subject.xxx}</c> placeholders.
/// </summary>
public enum AbacFilterDialect
{
    /// <summary>MongoDB query document (<see cref="Compiled.CompiledRule.MongoDbFilterJson"/>).</summary>
    MongoDb = 0,

    /// <summary>Elasticsearch Query DSL clause (<see cref="Compiled.CompiledRule.SearchFilter"/>).</summary>
    Elasticsearch = 1
}

/// <summary>
/// Resolves subject attributes and substitutes cross-attribute variables
/// (<c>{subject.xxx}</c>, §17.3) per request.
/// <para>
/// Supported fields: see <see cref="AbacSubjectVariables.SupportedFields"/>. <c>user.id</c> and
/// <c>user.email</c> are single-valued; <c>roles</c>, <c>teams.id</c> and <c>teams.name</c> are
/// multi-valued. An index-0 access like <c>teams[0].name</c> resolves to the first element only.
/// </para>
/// <para>
/// Substitution rules for multi-valued fields in filter JSON:
/// <list type="bullet">
///   <item>MongoDB: <c>$eq</c> becomes <c>$in</c>, <c>$ne</c> becomes <c>$nin</c>, an implicit
///   equality <c>{"field": "{subject.roles}"}</c> becomes <c>{"field": {"$in": [...]}}</c>.</item>
///   <item>Elasticsearch: <c>term</c> becomes <c>terms</c> with an array value.</item>
///   <item>Placeholders inside arrays (e.g. <c>$in</c>/<c>terms</c>) are expanded in place.</item>
///   <item>Limitation: range operators (<c>$gt</c>, <c>range</c>, ...) with a multi-valued
///   variable receive the JSON array verbatim — valid JSON, but semantically not supported.</item>
/// </list>
/// Unknown fields resolve to no value (scalar <c>null</c> / empty array), which fails closed
/// for allow rules.
/// </para>
/// </summary>
public static partial class AbacSubjectVariableResolver
{
    private const string PlaceholderMarker = "{subject.";

    [GeneratedRegex(@"^([a-zA-Z0-9_]+)\[0\]\.([a-zA-Z0-9_.]+)$")]
    private static partial Regex IndexedFieldRegex();

    /// <summary>
    /// Resolves a subject field path to its values. Multi-valued fields are expanded.
    /// Unknown fields resolve to an empty list.
    /// </summary>
    public static IReadOnlyList<object?> Resolve(string field, AbacSubject subject)
    {
        var indexed = IndexedFieldRegex().Match(field);
        if (indexed.Success)
        {
            var all = Resolve($"{indexed.Groups[1].Value}.{indexed.Groups[2].Value}", subject);
            return all.Count > 0 ? [all[0]] : [];
        }

        return field switch
        {
            "user.id" => [subject.User.Id],
            "user.email" => [subject.User.Email],
            "roles" => subject.Roles.Cast<object?>().ToList(),
            "teams.id" => subject.Teams.Select(t => (object?)t.Id).ToList(),
            "teams.name" => subject.Teams.Select(t => (object?)t.Name).ToList(),
            _ => []
        };
    }

    /// <summary>Returns <c>true</c> when the field resolves to a list of values (roles, teams.*).</summary>
    public static bool IsMultiValued(string field)
        => !IndexedFieldRegex().IsMatch(field) && field is "roles" or "teams.id" or "teams.name";

    /// <summary>
    /// Substitutes a condition value: a <c>{subject.xxx}</c> placeholder becomes the scalar value
    /// (single-valued fields) or a list (multi-valued fields). Non-placeholder values are returned unchanged.
    /// </summary>
    public static object? SubstituteValue(object? value, AbacSubject subject)
    {
        if (!AbacSubjectVariables.TryGetField(AbacValueComparer.Normalize(value), out var field))
            return value;

        var values = Resolve(field, subject);
        if (IsMultiValued(field))
            return values.ToList();

        return values.Count > 0 ? values[0] : null;
    }

    /// <summary>Returns <c>true</c> when the JSON contains at least one <c>{subject.</c> placeholder.</summary>
    public static bool ContainsPlaceholder(string? json)
        => json != null && json.Contains(PlaceholderMarker, StringComparison.Ordinal);

    /// <summary>
    /// Substitutes all <c>{subject.xxx}</c> placeholders in a filter JSON string for the given subject.
    /// Returns the input unchanged when it contains no placeholder.
    /// </summary>
    public static string? SubstituteFilterJson(string? json, AbacSubject subject, AbacFilterDialect dialect)
    {
        if (string.IsNullOrWhiteSpace(json) || !ContainsPlaceholder(json))
            return json;

        var root = JsonNode.Parse(json);
        if (root == null)
            return json;

        var rewritten = Rewrite(root, subject, dialect);
        return rewritten?.ToJsonString() ?? "null";
    }

    // ── JSON rewriting ────────────────────────────────────────────────────────

    private static JsonNode? Rewrite(JsonNode? node, AbacSubject subject, AbacFilterDialect dialect)
    {
        switch (node)
        {
            case JsonObject obj:
                return RewriteObject(obj, subject, dialect);
            case JsonArray arr:
                return RewriteArray(arr, subject, dialect);
            case JsonValue val when TryGetPlaceholder(val, out var field):
                return ToNode(field, subject);
            default:
                return node?.DeepClone();
        }
    }

    private static JsonObject RewriteObject(JsonObject obj, AbacSubject subject, AbacFilterDialect dialect)
    {
        var result = new JsonObject();

        foreach (var (key, child) in obj)
        {
            // Elasticsearch: term → terms when the term value is a multi-valued placeholder.
            if (dialect == AbacFilterDialect.Elasticsearch && key == "term" && child is JsonObject termObj
                && TryRewriteTerm(termObj, subject, out var termsObj))
            {
                result["terms"] = termsObj;
                continue;
            }

            if (child is JsonValue val && TryGetPlaceholder(val, out var field))
            {
                var replacement = ToNode(field, subject);

                if (dialect == AbacFilterDialect.MongoDb && IsMultiValued(field))
                {
                    switch (key)
                    {
                        case "$eq":
                            result["$in"] = replacement;
                            continue;
                        case "$ne":
                            result["$nin"] = replacement;
                            continue;
                        default:
                            if (!key.StartsWith('$'))
                            {
                                // Implicit equality: { field: [..] } would mean "array equals" → use $in.
                                result[key] = new JsonObject { ["$in"] = replacement };
                                continue;
                            }
                            break;
                    }
                }

                result[key] = replacement;
                continue;
            }

            result[key] = Rewrite(child, subject, dialect);
        }

        return result;
    }

    private static bool TryRewriteTerm(JsonObject termObj, AbacSubject subject, out JsonObject termsObj)
    {
        termsObj = null!;
        if (termObj.Count != 1)
            return false;

        var (fieldName, value) = termObj.First();

        // Short form { "term": { "field": "{subject.roles}" } } or long form { "term": { "field": { "value": "{subject.roles}" } } }
        var valueNode = value is JsonObject longForm && longForm.TryGetPropertyValue("value", out var v) ? v : value;
        if (valueNode is not JsonValue jv || !TryGetPlaceholder(jv, out var field) || !IsMultiValued(field))
            return false;

        termsObj = new JsonObject { [fieldName] = ToNode(field, subject) };
        return true;
    }

    private static JsonArray RewriteArray(JsonArray arr, AbacSubject subject, AbacFilterDialect dialect)
    {
        var result = new JsonArray();
        foreach (var item in arr)
        {
            if (item is JsonValue val && TryGetPlaceholder(val, out var field))
            {
                foreach (var resolved in Resolve(field, subject))
                    result.Add(ToScalarNode(resolved));
                continue;
            }

            result.Add(Rewrite(item, subject, dialect));
        }

        return result;
    }

    private static bool TryGetPlaceholder(JsonValue value, out string field)
    {
        field = string.Empty;
        return value.TryGetValue<string>(out var s) && AbacSubjectVariables.TryGetField(s, out field);
    }

    private static JsonNode? ToNode(string field, AbacSubject subject)
    {
        var values = Resolve(field, subject);
        if (IsMultiValued(field))
            return new JsonArray(values.Select(ToScalarNode).ToArray());

        return values.Count > 0 ? ToScalarNode(values[0]) : null;
    }

    private static JsonNode? ToScalarNode(object? value) => value switch
    {
        null => null,
        Guid g => JsonValue.Create(g.ToString()),
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        long l => JsonValue.Create(l),
        int i => JsonValue.Create(i),
        double d => JsonValue.Create(d),
        _ => JsonValue.Create(value.ToString())
    };
}

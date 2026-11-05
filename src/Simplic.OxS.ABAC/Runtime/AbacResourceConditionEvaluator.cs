using System.Collections;
using System.Reflection;
using System.Text.Json;
using Simplic.OxS.ABAC.Ast;
using Simplic.OxS.ABAC.Subject;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Evaluates resource conditions (<see cref="ConditionNode"/> AST) in-process against a concrete
/// resource, e.g. for single-resource <c>Authorize</c> checks or SignalR broadcast filtering.
/// <para>
/// The resource is a dictionary keyed by <b>policy field names</b>. Dot paths
/// (<c>owner.department</c>) navigate nested dictionaries, <see cref="JsonElement"/> objects and
/// plain CLR objects (public properties, case-insensitive). Arrays along the path are flattened,
/// so operators match when <i>any</i> element satisfies them (MongoDB semantics).
/// </para>
/// <para>
/// Condition values that are <c>{subject.xxx}</c> placeholders (§17.3) are substituted from the
/// subject before comparison; a multi-valued substitution turns <c>$eq</c> into <c>$in</c> and
/// <c>$ne</c> into <c>$nin</c>.
/// </para>
/// Missing fields resolve to no values: <c>$eq</c>/<c>$in</c>/<c>$contains</c>/range operators are
/// then <c>false</c>, the negated operators <c>true</c> (consistent with MongoDB).
/// </summary>
public static class AbacResourceConditionEvaluator
{
    /// <summary>
    /// Returns <c>true</c> when the condition tree is satisfied by <paramref name="resource"/>.
    /// A <c>null</c> node is treated as "always true" (no conditions).
    /// </summary>
    /// <param name="node">Resource condition AST.</param>
    /// <param name="resource">Resource attributes keyed by policy field name.</param>
    /// <param name="subject">Subject used to resolve <c>{subject.xxx}</c> placeholders; when <c>null</c>, placeholders resolve to no value.</param>
    public static bool Evaluate(ConditionNode? node, IReadOnlyDictionary<string, object?> resource, AbacSubject? subject = null)
    {
        if (node == null)
            return true;

        return node.NodeType switch
        {
            ConditionNodeType.And => node.Children.All(c => Evaluate(c, resource, subject)),
            ConditionNodeType.Or => node.Children.Any(c => Evaluate(c, resource, subject)),
            ConditionNodeType.Comparison => EvaluateComparison(node, resource, subject),
            _ => false
        };
    }

    private static bool EvaluateComparison(ConditionNode node, IReadOnlyDictionary<string, object?> resource, AbacSubject? subject)
    {
        if (string.IsNullOrEmpty(node.Field) || string.IsNullOrEmpty(node.Operator))
            return false;

        var op = node.Operator;
        var expected = node.Value;

        if (AbacSubjectVariables.TryGetField(AbacValueComparer.Normalize(expected), out var variableField))
        {
            if (subject == null)
            {
                expected = AbacSubjectVariableResolver.IsMultiValued(variableField) ? new List<object?>() : null;
            }
            else
            {
                expected = AbacSubjectVariableResolver.SubstituteValue(expected, subject);
            }

            if (AbacValueComparer.IsList(expected))
            {
                op = op switch
                {
                    "$eq" => "$in",
                    "$ne" => "$nin",
                    _ => op
                };
            }
        }
        else if (subject != null && AbacValueComparer.IsList(AbacValueComparer.Normalize(expected)))
        {
            // Expand placeholders inside $in / $nin lists.
            expected = ExpandList(AbacValueComparer.Normalize(expected)!, subject);
        }

        var values = ResolvePath(resource, node.Field);
        var rawValues = op is "$contains" or "$ncontains" ? ResolvePath(resource, node.Field, flattenLeaves: false) : values;

        return op switch
        {
            "$eq" => values.Any(v => AbacValueComparer.EqualsCi(v, expected)),
            "$ne" => !values.Any(v => AbacValueComparer.EqualsCi(v, expected)),
            "$in" => values.Any(v => AbacValueComparer.ContainedIn(v, expected)),
            "$nin" => !values.Any(v => AbacValueComparer.ContainedIn(v, expected)),
            "$contains" => ContainsAny(rawValues, expected),
            "$ncontains" => !ContainsAny(rawValues, expected),
            "$gt" => values.Any(v => AbacValueComparer.Compare(v, expected) > 0),
            "$gte" => values.Any(v => AbacValueComparer.Compare(v, expected) >= 0),
            "$lt" => values.Any(v => AbacValueComparer.Compare(v, expected) < 0),
            "$lte" => values.Any(v => AbacValueComparer.Compare(v, expected) <= 0),
            _ => false
        };
    }

    private static bool ContainsAny(IReadOnlyList<object?> values, object? needle)
    {
        // $contains on an array field: any element equals the needle; on a string: substring match.
        foreach (var v in values)
        {
            if (AbacValueComparer.ContainsValue(v, needle) || AbacValueComparer.EqualsCi(v, needle))
                return true;
        }

        return false;
    }

    private static List<object?> ExpandList(object list, AbacSubject subject)
    {
        var result = new List<object?>();
        foreach (var item in (IEnumerable)list)
        {
            var normalized = AbacValueComparer.Normalize(item);
            if (AbacSubjectVariables.TryGetField(normalized, out var field))
                result.AddRange(AbacSubjectVariableResolver.Resolve(field, subject));
            else
                result.Add(normalized);
        }

        return result;
    }

    // ── Path resolution ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a dot path against the resource. Exact keys (including keys containing dots) win
    /// over path navigation. Collections are flattened into individual values.
    /// </summary>
    internal static IReadOnlyList<object?> ResolvePath(IReadOnlyDictionary<string, object?> resource, string path, bool flattenLeaves = true)
    {
        if (TryGetCi(resource, path, out var direct))
            return flattenLeaves ? Flatten(direct) : [AbacValueComparer.Normalize(direct)];

        var current = new List<object?> { resource };
        foreach (var segment in path.Split('.'))
        {
            var next = new List<object?>();
            foreach (var item in current)
            {
                foreach (var candidate in Flatten(item))
                {
                    if (TryGetMember(candidate, segment, out var value))
                        next.Add(value);
                }
            }

            if (next.Count == 0)
                return [];

            current = next;
        }

        return flattenLeaves ? current.SelectMany(Flatten).ToList() : current.Select(AbacValueComparer.Normalize).ToList();
    }

    private static IReadOnlyList<object?> Flatten(object? value)
    {
        value = value is JsonElement ? AbacValueComparer.Normalize(value) : value;
        if (!AbacValueComparer.IsList(value))
            return [value];

        var result = new List<object?>();
        foreach (var item in (IEnumerable)value!)
            result.AddRange(Flatten(item));
        return result;
    }

    private static bool TryGetMember(object? target, string name, out object? value)
    {
        value = null;
        switch (target)
        {
            case null:
                return false;
            case IReadOnlyDictionary<string, object?> ro:
                return TryGetCi(ro, name, out value);
            case IDictionary<string, object?> rw:
                return TryGetCi(rw, name, out value);
            case IDictionary dict:
                foreach (DictionaryEntry entry in dict)
                {
                    if (string.Equals(entry.Key?.ToString(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = entry.Value;
                        return true;
                    }
                }
                return false;
            case string or ValueType:
                return false;
            default:
                var prop = target.GetType().GetProperty(name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || prop.GetIndexParameters().Length > 0)
                    return false;
                value = prop.GetValue(target);
                return true;
        }
    }

    private static bool TryGetCi(IEnumerable<KeyValuePair<string, object?>> dict, string key, out object? value)
    {
        if (dict is IReadOnlyDictionary<string, object?> ro && ro.TryGetValue(key, out value))
            return true;

        foreach (var kv in dict)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}

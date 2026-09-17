using System.Globalization;
using OxQL.Model;
using OxQL.Model.Addon;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// What an addon definition must satisfy before it is stored: a registered, extendable entity;
/// a path of plain segments under the bag; a known kind; a value list only where a set filter
/// can use it; and no shadowing between a scalar and its descendants. Path and kind are
/// immutable after creation, so every rule here is checked on creation and the value rule
/// again on update. Each check answers the refusal's sentence, or null when the input passes.
/// </summary>
public static class AddonDefinitionRules
{
    /// <summary>The kinds in their wire spelling, the schema's, and the engine's kind for each.</summary>
    private static readonly Dictionary<string, AddonKind> KindsByName = new(StringComparer.Ordinal)
    {
        ["string"] = AddonKind.String,
        ["int"] = AddonKind.Int,
        ["long"] = AddonKind.Long,
        ["double"] = AddonKind.Double,
        ["decimal"] = AddonKind.Decimal,
        ["bool"] = AddonKind.Bool,
        ["date"] = AddonKind.Date,
        ["dateTime"] = AddonKind.DateTime,
        ["guid"] = AddonKind.Guid,
        ["object"] = AddonKind.Object,
    };

    private static readonly Dictionary<AddonKind, string> NamesByKind = KindsByName.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>The kind spellings a definition may declare, in a fixed order.</summary>
    public static IReadOnlyList<string> KindNames { get; } = [.. KindsByName.Keys];

    /// <summary>Parses a kind spelling exactly; null when it is not a kind.</summary>
    public static AddonKind? ParseKind(string? name) =>
        name is not null && KindsByName.TryGetValue(name, out var kind) ? kind : null;

    /// <summary>The wire spelling of a kind.</summary>
    public static string KindName(AddonKind kind) => NamesByKind[kind];

    /// <summary>The entity must be one the host declares and it must carry an addon bag.</summary>
    public static string? CheckEntity(EntityModel model, string? entity)
    {
        if (string.IsNullOrWhiteSpace(entity))
            return "The entity id is required.";

        if (!model.Entities.TryGetValue(entity, out var definition))
            return $"'{entity}' is not an entity of this service.";

        if (!definition.Extendable)
            return $"'{entity}' is not extendable, so it has no addon bag to define keys under.";

        return null;
    }

    /// <summary>
    /// The path is the storage path under the bag, verbatim: dot-separated, every segment
    /// non-empty and not starting with <c>$</c>; a segment may contain spaces but neither
    /// starts nor ends with one. A key containing a dot cannot be defined.
    /// </summary>
    public static string? CheckPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "The path is required.";

        foreach (var segment in path.Split('.'))
        {
            if (segment.Length == 0)
                return $"'{path}' has an empty segment; a key containing a dot cannot be defined.";

            if (segment[0] == '$')
                return $"'{path}' has a segment starting with '$', which cannot be defined.";

            if (segment != segment.Trim())
                return $"'{path}' has a segment with leading or trailing whitespace.";
        }

        return null;
    }

    /// <summary>The kind must be one of <see cref="KindNames"/>.</summary>
    public static string? CheckKind(string? kind) =>
        ParseKind(kind) is null ? $"'{kind}' is not a kind; one of {string.Join(", ", KindNames)} is." : null;

    /// <summary>A closed value list is allowed on string and int keys only, and every int value must be an integer.</summary>
    public static string? CheckValues(AddonKind kind, IReadOnlyList<AddonDefinitionValue>? values)
    {
        if (values is null || values.Count == 0)
            return null;

        if (kind is not (AddonKind.String or AddonKind.Int))
            return $"A value list is only allowed on string and int keys, not on {KindName(kind)}.";

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value.Value))
                return "Every value of the list must be non-empty.";

            if (kind == AddonKind.Int && !long.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                return $"'{value.Value}' is not an integer, which an int key's values must be.";
        }

        return null;
    }

    /// <summary>
    /// A path defined with a scalar kind cannot have defined descendants, and a scalar cannot
    /// be defined above an existing definition; an <c>object</c> parent with defined children
    /// is the normal subobject case. Retired definitions shadow nothing.
    /// </summary>
    public static string? CheckShadowing(string path, AddonKind kind, IEnumerable<AddonDefinitionDocument> existing)
    {
        foreach (var other in existing)
        {
            if (other.Retired || string.Equals(other.Path, path, StringComparison.Ordinal))
                continue;

            var otherKind = ParseKind(other.Kind) ?? AddonKind.Object;

            if (otherKind != AddonKind.Object && path.StartsWith(other.Path + ".", StringComparison.Ordinal))
                return $"'{other.Path}' is defined as {KindName(otherKind)}; a scalar cannot have defined descendants.";

            if (kind != AddonKind.Object && other.Path.StartsWith(path + ".", StringComparison.Ordinal))
                return $"'{path}' would shadow the defined key '{other.Path}'; define it as object or retire the descendant first.";
        }

        return null;
    }
}

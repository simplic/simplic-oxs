using Simplic.OxS.ABAC.Contract;

namespace Simplic.OxS.ABAC.Mapping;

/// <summary>
/// One resource field of the per-organisation field mapping set (§12.7).
/// Identity is the source of truth; search and services receive synced copies.
/// </summary>
public class AbacFieldDefinition
{
    /// <summary>Policy field name used in resource conditions, e.g. <c>tenantGroup</c>.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Field kind; determines the derived search path.</summary>
    public AbacFieldType Type { get; set; } = AbacFieldType.Attribute;

    /// <summary>Key inside the <c>security</c> block, e.g. <c>tenant_group</c>.</summary>
    public string SecurityKey { get; set; } = string.Empty;

    /// <summary>MongoDB document path used by egress filters. <c>null</c> = not filterable in MongoDB.</summary>
    public string? MongoDbPath { get; set; }

    /// <summary>
    /// JSON paths used by the search indexer to materialise the value, keyed by data type.
    /// The empty key <c>""</c> is the default for all data types.
    /// </summary>
    public Dictionary<string, string> JsonPaths { get; set; } = [];

    /// <summary>Derived Elasticsearch field path.</summary>
    public string SearchPath => Type switch
    {
        AbacFieldType.Principal => "security.principals",
        AbacFieldType.Classification => "security.classification",
        _ => $"security.attributes.{SecurityKey}"
    };

    /// <summary>
    /// Derived value prefix (principals only), without the separator: the search filter
    /// builder emits <c>{prefix}:{value}</c>.
    /// </summary>
    public string? SearchValuePrefix => Type == AbacFieldType.Principal ? SecurityKey : null;

    /// <summary>Converts to the compiler-facing <see cref="AbacFieldMapping"/>.</summary>
    public AbacFieldMapping ToFieldMapping() => new()
    {
        PolicyField = Field,
        MongoDbPath = MongoDbPath,
        SearchPath = SearchPath,
        SearchValuePrefix = SearchValuePrefix
    };

    /// <summary>Resolves the JSON path for <paramref name="dataType"/> falling back to the default entry.</summary>
    public string? ResolveJsonPath(string? dataType)
    {
        if (!string.IsNullOrEmpty(dataType) && JsonPaths.TryGetValue(dataType, out var path))
            return path;
        return JsonPaths.TryGetValue(string.Empty, out var fallback) ? fallback : null;
    }
}
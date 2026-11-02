using Simplic.OxS.ABAC.Contract;

namespace Simplic.OxS.ABAC.Mapping;

/// <summary>
/// Per-organisation field mapping set (Identity collection <c>abac.field-mappings</c>).
/// </summary>
public class AbacFieldMappingSet
{
    /// <summary>Organisation id.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Monotonically increasing version, incremented on every change.</summary>
    public long Version { get; set; }

    /// <summary>UTC timestamp of the last change.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Field definitions.</summary>
    public List<AbacFieldDefinition> Fields { get; set; } = [];

    /// <summary>Compiler-facing dictionary keyed by policy field (case-insensitive).</summary>
    public IReadOnlyDictionary<string, AbacFieldMapping> ToFieldMappings()
        => Fields
            .Where(f => !string.IsNullOrWhiteSpace(f.Field))
            .GroupBy(f => f.Field, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().ToFieldMapping(), StringComparer.OrdinalIgnoreCase);
}
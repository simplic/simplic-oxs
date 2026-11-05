namespace Simplic.OxS.ABAC.Sync;

/// <summary>
/// Message-bus event published by Identity after the per-organisation field mapping set
/// changed. Trigger only; consumers pull <see cref="AbacSyncEndpoints.FieldMappings"/>.
/// </summary>
public class AbacFieldMappingsChanged
{
    /// <summary>Organisation whose mappings changed.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>New monotonically increasing mapping version.</summary>
    public long Version { get; set; }

    /// <summary>UTC change timestamp.</summary>
    public DateTime ChangedAt { get; set; }
}
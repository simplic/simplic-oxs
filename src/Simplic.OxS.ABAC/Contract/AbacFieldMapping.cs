namespace Simplic.OxS.ABAC.Contract;

/// <summary>
/// Maps a policy condition field name to its backend-specific storage paths.
/// Registered in <see cref="IAbacCompileContract.FieldMappings"/>.
/// </summary>
public class AbacFieldMapping
{
    /// <summary>
    /// Field name as used in JSON policy conditions (e.g. <c>department</c>, <c>status</c>).
    /// </summary>
    public string PolicyField { get; set; } = string.Empty;

    /// <summary>
    /// MongoDB document field path (e.g. <c>department</c>, <c>metadata.status</c>).
    /// Null when the field is not queryable via MongoDB.
    /// </summary>
    public string? MongoDbPath { get; set; }

    /// <summary>
    /// Search index field path (e.g. <c>department</c>, <c>tags</c>).
    /// Null when the field is not indexed in the search backend.
    /// </summary>
    public string? SearchPath { get; set; }

    /// <summary>
    /// Optional value prefix for principal-style search fields. When set, scalar condition
    /// values are emitted as <c>{SearchValuePrefix}:{value}</c> and <see cref="SearchPath"/>
    /// points at the shared principals field (e.g. <c>security.principals</c>), mirroring
    /// index-time materialization (ABAC concept §18.11). Range operators are not expressible
    /// against prefixed fields and fail closed.
    /// </summary>
    public string? SearchValuePrefix { get; set; }
}

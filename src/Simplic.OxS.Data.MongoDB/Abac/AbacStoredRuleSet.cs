using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Simplic.OxS.Data.MongoDB.Abac;

/// <summary>
/// Service-local, durable copy of an organization's compiled ABAC rule set (§12.6),
/// stored in the collection <c>abac.compiled-rules</c>.
/// <para>
/// The full rule set as delivered by Identity is kept as a JSON string in
/// <see cref="PayloadJson"/> (serialized <c>Simplic.OxS.ABAC.Sync.AbacOrganizationRuleSet</c>)
/// to avoid BSON mapping problems with <c>object</c>/dictionary-typed condition values.
/// Filtering on the service's own action prefixes happens when reading.
/// </para>
/// Only the sync component writes this collection; retention keeps the active and the previous set.
/// </summary>
[BsonIgnoreExtraElements]
public class AbacStoredRuleSet
{
    /// <summary>Document id: <c>{organizationId:N}:{setId}</c>.</summary>
    [BsonId]
    public string Id { get; set; } = string.Empty;

    /// <summary>Owning organization (all entries are organization-scoped).</summary>
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid OrganizationId { get; set; }

    /// <summary>Set id as delivered by Identity.</summary>
    public string SetId { get; set; } = string.Empty;

    /// <summary>Engine schema version the set was compiled with.</summary>
    public string EngineVersion { get; set; } = string.Empty;

    /// <summary>UTC time of the last successful pull of this set (basis for Max-Age / Hard-Limit).</summary>
    public DateTime FetchedAt { get; set; }

    /// <summary>UTC time the set was activated in Identity.</summary>
    public DateTime ActivatedAt { get; set; }

    /// <summary>Whether this is the currently active set of the organization.</summary>
    public bool IsActive { get; set; }

    /// <summary>Identity confirmed that the organization has no policies (§6.4).</summary>
    public bool IsConfirmedEmpty { get; set; }

    /// <summary>Effective rollout flag delivered with the set (§6.4).</summary>
    public bool AllowAllWhenEmpty { get; set; }

    /// <summary>The full rule set serialized as JSON (web defaults, camelCase).</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>Builds the document id for an organization/set combination.</summary>
    public static string BuildId(Guid organizationId, string setId) => $"{organizationId:N}:{setId}";
}

using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OxQL.Core.Engine;
using Simplic.OxS.Server.OxSchema;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// The part of the client that reads which entities an owner publishes, for the query engine's
/// check of this host's declared references against the owners' schema documents
/// (<see cref="IRemoteSchemaSource"/>; the engine's <c>RemoteReferenceValidator</c> finds it on
/// the registered <see cref="IRemoteQueryClient"/>).
/// <para>
/// One <c>GET {route}/schema</c> per owner, over the same route as every other owner call
/// (<see cref="RouteOf"/>: <c>InternalHosts</c>, <c>InternalApiVersions</c>). The document is
/// anonymous, so the call carries no key and no identity. It is conditional on what this client
/// last read of the owner (<c>If-None-Match</c>), so an owner whose document did not change
/// answers 304 without a body, and what was read is kept: within <see cref="SchemaTtl"/> the
/// owner is not asked again at all, whatever the last read gave.
/// </para>
/// <para>
/// The read never throws and never waits longer than <see cref="SchemaBudget"/>. An owner that
/// does not answer, answers anything but 200 or 304 (404 from an owner without the route or
/// reached on the wrong api version, 401 from one that requires authorization for its document),
/// or answers a body that is no schema document is "not known" (null): its references stay not
/// validated, never wrong.
/// </para>
/// </summary>
public sealed partial class RemoteQueryClient : IRemoteSchemaSource
{
    /// <summary>The bound of one read of an owner's schema document.</summary>
    internal TimeSpan SchemaBudget { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long what was read of an owner's schema document answers without asking the owner
    /// again, a failed read included; null is <see cref="FactsTtl"/>, the time what an owner's
    /// health said of it stays known.
    /// </summary>
    internal TimeSpan? SchemaTtl { get; set; }

    private readonly ConcurrentDictionary<string, OwnerSchema> schemas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What was read of an owner's schema document: the entities of the last document that read,
    /// with the entity tag it was served under, and the reason the last read gave none, if it did
    /// not. The entities outlive a failed read as the validator of the next one, and answer
    /// nobody meanwhile.
    /// </summary>
    private sealed record OwnerSchema(RemoteSchemaEntities? Entities, string? EntityTag, string? Reason, DateTimeOffset ReadAt);

    /// <summary>The owner's schema document route for a service key, or null when the host does not know the service.</summary>
    public string? SchemaUrl(string serviceKey) => Url(serviceKey, "schema");

    /// <inheritdoc/>
    public async Task<RemoteSchemaEntities?> SchemaEntitiesAsync(string serviceKey, string? knownRevision, CancellationToken cancellationToken)
    {
        if (SchemaUrl(serviceKey) is not { } url)
            return null;

        schemas.TryGetValue(serviceKey, out var kept);

        if (kept is not null && Time.GetUtcNow() - kept.ReadAt < (SchemaTtl ?? FactsTtl))
            return kept.Reason is null ? Answer(kept.Entities, knownRevision) : null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
            return Unknown(serviceKey, kept, "address", null);

        // What this client holds is the validator; without it, the revision the caller holds.
        var validator = kept?.Entities is not null ? kept.EntityTag : EntityTagOf(knownRevision);

        try
        {
            using var timeout = Timeout(SchemaBudget, cancellationToken);
            using var message = new HttpRequestMessage(HttpMethod.Get, address);

            if (validator is not null)
                message.Headers.TryAddWithoutValidation("If-None-Match", validator);

            using var response = await clients.CreateClient(HttpClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (response.StatusCode == HttpStatusCode.NotModified && validator is not null)
            {
                if (kept?.Entities is null)
                    return RemoteSchemaEntities.NotModified(knownRevision!);

                schemas[serviceKey] = kept with { Reason = null, ReadAt = Time.GetUtcNow() };

                return Answer(kept.Entities, knownRevision);
            }

            if (response.StatusCode != HttpStatusCode.OK)
                return Unknown(serviceKey, kept, $"status {(int)response.StatusCode}", null);

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token);

            if (EntitiesOf(document.RootElement) is not { } entities)
                return Unknown(serviceKey, kept, "document", null);

            schemas[serviceKey] = new OwnerSchema(entities, response.Headers.ETag?.ToString() ?? EntityTagOf(entities.Revision), null, Time.GetUtcNow());

            return Answer(entities, knownRevision);
        }
        catch (JsonException exception)
        {
            return Unknown(serviceKey, kept, "document", exception);
        }
        catch (Exception exception)
        {
            // Nothing a read of another service's document meets may fail what asked for it; a caller that gave up learns nothing either.
            return cancellationToken.IsCancellationRequested ? null : Unknown(serviceKey, kept, "unreachable", exception);
        }
    }

    /// <summary>What a caller that holds <paramref name="knownRevision"/> is told of <paramref name="entities"/>.</summary>
    private static RemoteSchemaEntities? Answer(RemoteSchemaEntities? entities, string? knownRevision) =>
        entities is { Revision: { } revision } && revision == knownRevision ? RemoteSchemaEntities.NotModified(revision) : entities;

    /// <summary>
    /// Keeps that the owner's document could not be read, so the owner is not asked again within
    /// the time to live, and says so once per reason: at warning for a document that does not
    /// read as one, since its owner answered and something is wrong with what it serves.
    /// </summary>
    private RemoteSchemaEntities? Unknown(string serviceKey, OwnerSchema? kept, string reason, Exception? exception)
    {
        schemas[serviceKey] = new OwnerSchema(kept?.Entities, kept?.EntityTag, reason, Time.GetUtcNow());

        if (kept?.Reason == reason)
            return null;

        if (reason == "document")
            logger.LogWarning(exception, "OxQL owner {Service} answered its schema route with a body that is no schema document; the references into it stay not validated", serviceKey);
        else
            logger.LogInformation("OxQL owner {Service} did not serve its schema document ({Reason}); the references into it stay not validated", serviceKey, reason);

        return null;
    }

    /// <summary>
    /// The revision of a schema document and every id its owner answers a query under: the ids of
    /// its entities and the ids they retired. An entity's legacy model ids (<c>$Name</c> among its
    /// aliases) name no entity of a query and are left out. Null for a body that is no schema document.
    /// </summary>
    private static RemoteSchemaEntities? EntitiesOf(JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object
            || !document.TryGetProperty("revision", out var revision) || revision.ValueKind != JsonValueKind.String
            || !document.TryGetProperty("types", out var types) || types.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var ids = new List<string>();

        foreach (var type in types.EnumerateObject())
        {
            if (type.Value.ValueKind != JsonValueKind.Object || !type.Value.TryGetProperty("entity", out var entity) || entity.ValueKind != JsonValueKind.True)
                continue;

            ids.Add(type.Name);

            if (!type.Value.TryGetProperty("aliases", out var aliases) || aliases.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var alias in aliases.EnumerateArray())
                if (alias.ValueKind == JsonValueKind.String && alias.GetString() is { Length: > 0 } id && !id.StartsWith('$'))
                    ids.Add(id);
        }

        return new RemoteSchemaEntities(revision.GetString(), ids);
    }

    /// <summary>The entity tag a document of <paramref name="revision"/> is served under, or null for a revision that is none.</summary>
    private static string? EntityTagOf(string? revision) =>
        revision is not null && revision.StartsWith("sha256:", StringComparison.Ordinal) && revision.Length > "sha256:".Length ? OxSchemaJson.EntityTag(revision) : null;
}

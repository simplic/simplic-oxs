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
/// owner is not asked again at all, whatever the last read gave. Callers that ask for an owner
/// while a read of it is under way share that read, which ends early only when the last of
/// them has given up.
/// </para>
/// <para>
/// The read never throws and never waits longer than <see cref="SchemaBudget"/>. An owner that
/// does not answer, answers anything but 200 or 304 (404 from an owner without the route or
/// reached on the wrong api version, 401 from one that requires authorization for its document),
/// or answers a body that is no schema document or has more than <see cref="SchemaBodyLimit"/>
/// bytes is "not known" (null): its references stay not validated, never wrong.
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

    /// <summary>
    /// The most bytes of a schema document the client reads. A document describes every type of
    /// a service and grows with its model; this bound is far above what a model comes to and
    /// still limits what one answer of another service makes this host hold. A larger body is
    /// not read to its end.
    /// </summary>
    internal long SchemaBodyLimit { get; set; } = 16 * 1024 * 1024;

    private readonly ConcurrentDictionary<string, OwnerSchema> schemas = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The reads under way, by owner and validator. Guarded by itself.</summary>
    private readonly Dictionary<string, SchemaFlight> schemaFlights = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// One read of an owner's schema document and the callers that wait for it. The read is on
    /// no caller's token: <see cref="Abandoned"/> cancels it once the last caller has stopped
    /// waiting, so a caller that gives up does not end it for the others.
    /// </summary>
    private sealed class SchemaFlight
    {
        private readonly Lazy<Task<SchemaRead>> read;

        public SchemaFlight(string key, Func<SchemaFlight, Task<SchemaRead>> start)
        {
            Key = key;
            read = new Lazy<Task<SchemaRead>>(() => start(this));
        }

        /// <summary>What the flight is registered under.</summary>
        public string Key { get; }

        /// <summary>Cancelled when nobody waits for the read any more.</summary>
        public CancellationTokenSource Abandoned { get; } = new();

        /// <summary>How many callers wait; guarded by the reads under way.</summary>
        public int Waiting { get; set; }

        /// <summary>Whether the read has ended; guarded by the reads under way.</summary>
        public bool Done { get; set; }

        /// <summary>The read, started by the first caller that asks for it.</summary>
        public Task<SchemaRead> Read => read.Value;
    }

    /// <summary>
    /// What one read gave its callers: the owner's entities; or that the owner answered 304 to
    /// the revision the callers hold while this client held nothing; or neither, an owner that
    /// is not known.
    /// </summary>
    private readonly record struct SchemaRead(RemoteSchemaEntities? Entities, bool CallerRevisionCurrent);

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

        SchemaFlight flight;

        // One lock over "is what was read still current" and "is a read under way": a read keeps
        // what it learned before it leaves the reads under way, so a caller sees one or the other.
        lock (schemaFlights)
        {
            schemas.TryGetValue(serviceKey, out var kept);

            if (kept is not null && Time.GetUtcNow() - kept.ReadAt < (SchemaTtl ?? FactsTtl))
                return kept.Reason is null ? Answer(kept.Entities, knownRevision) : null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
                return Unknown(serviceKey, kept, "address", null);

            // What this client holds is the validator; without it, the revision the caller holds.
            // Callers share a read of the same validator: the answer to it is the answer to each.
            var validator = kept?.Entities is not null ? kept.EntityTag : EntityTagOf(knownRevision);
            var key = $"{serviceKey}\n{validator}";

            if (!schemaFlights.TryGetValue(key, out flight!))
                schemaFlights[key] = flight = new SchemaFlight(key, started => ReadSchemaAsync(serviceKey, address, kept, validator, started));

            flight.Waiting++;
        }

        try
        {
            var read = await flight.Read.WaitAsync(cancellationToken);

            return read.CallerRevisionCurrent ? RemoteSchemaEntities.NotModified(knownRevision!) : Answer(read.Entities, knownRevision);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A caller that gave up learns nothing.
            return null;
        }
        finally
        {
            Leave(flight);
        }
    }

    /// <summary>
    /// One caller stops waiting for a read. The read of an owner nobody waits for any more is
    /// cancelled and keeps nothing, so the next caller asks the owner anew.
    /// </summary>
    private void Leave(SchemaFlight flight)
    {
        lock (schemaFlights)
        {
            if (--flight.Waiting > 0 || flight.Done)
                return;

            if (schemaFlights.TryGetValue(flight.Key, out var current) && ReferenceEquals(current, flight))
                schemaFlights.Remove(flight.Key);
        }

        flight.Abandoned.Cancel();
    }

    /// <summary>
    /// The read the callers of one <see cref="SchemaFlight"/> share: one conditional GET, bounded
    /// by <see cref="SchemaBudget"/> and by <paramref name="flight"/>'s callers giving up. It
    /// keeps what it learned, a failure included, and never throws.
    /// </summary>
    private async Task<SchemaRead> ReadSchemaAsync(string serviceKey, Uri address, OwnerSchema? kept, string? validator, SchemaFlight flight)
    {
        var abandoned = flight.Abandoned.Token;

        try
        {
            using var timeout = Timeout(SchemaBudget, abandoned);
            using var message = new HttpRequestMessage(HttpMethod.Get, address);

            if (validator is not null)
                message.Headers.TryAddWithoutValidation("If-None-Match", validator);

            using var response = await clients.CreateClient(HttpClientName).SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (response.StatusCode == HttpStatusCode.NotModified && validator is not null)
            {
                if (kept?.Entities is null)
                    return new SchemaRead(null, CallerRevisionCurrent: true);

                schemas[serviceKey] = kept with { Reason = null, ReadAt = Time.GetUtcNow() };

                return new SchemaRead(kept.Entities, false);
            }

            if (response.StatusCode != HttpStatusCode.OK)
                return new SchemaRead(Unknown(serviceKey, kept, $"status {(int)response.StatusCode}", null), false);

            if (response.Content.Headers.ContentLength > SchemaBodyLimit || await BodyOfAsync(response, timeout.Token) is not { } body)
                return new SchemaRead(Unknown(serviceKey, kept, "size", null), false);

            using var document = JsonDocument.Parse(body);

            if (EntitiesOf(document.RootElement) is not { } entities)
                return new SchemaRead(Unknown(serviceKey, kept, "document", null), false);

            schemas[serviceKey] = new OwnerSchema(entities, response.Headers.ETag?.ToString() ?? EntityTagOf(entities.Revision), null, Time.GetUtcNow());

            return new SchemaRead(entities, false);
        }
        catch (JsonException exception)
        {
            return new SchemaRead(Unknown(serviceKey, kept, "document", exception), false);
        }
        catch (Exception exception)
        {
            // Nothing a read of another service's document meets may fail what asked for it; a read nobody waits for learns nothing either.
            return new SchemaRead(abandoned.IsCancellationRequested ? null : Unknown(serviceKey, kept, "unreachable", exception), false);
        }
        finally
        {
            lock (schemaFlights)
            {
                flight.Done = true;

                if (schemaFlights.TryGetValue(flight.Key, out var current) && ReferenceEquals(current, flight))
                    schemaFlights.Remove(flight.Key);
            }
        }
    }

    /// <summary>The body of an answer, or null for one of more than <see cref="SchemaBodyLimit"/> bytes, which is not read to its end.</summary>
    private async Task<ReadOnlyMemory<byte>?> BodyOfAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        using var body = new MemoryStream();
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            while (await stream.ReadAsync(buffer, cancellationToken) is > 0 and var read)
            {
                if (body.Length + read > SchemaBodyLimit)
                    return null;

                body.Write(buffer, 0, read);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        return body.GetBuffer().AsMemory(0, (int)body.Length);
    }

    /// <summary>What a caller that holds <paramref name="knownRevision"/> is told of <paramref name="entities"/>.</summary>
    private static RemoteSchemaEntities? Answer(RemoteSchemaEntities? entities, string? knownRevision) =>
        entities is { Revision: { } revision } && revision == knownRevision ? RemoteSchemaEntities.NotModified(revision) : entities;

    /// <summary>
    /// Keeps that the owner's document could not be read, so the owner is not asked again within
    /// the time to live, and says so once per reason: at warning for a document that does not
    /// read as one or is larger than the client reads, since its owner answered and something
    /// is wrong with what it serves.
    /// </summary>
    private RemoteSchemaEntities? Unknown(string serviceKey, OwnerSchema? kept, string reason, Exception? exception)
    {
        schemas[serviceKey] = new OwnerSchema(kept?.Entities, kept?.EntityTag, reason, Time.GetUtcNow());

        if (kept?.Reason == reason)
            return null;

        if (reason == "document")
            logger.LogWarning(exception, "OxQL owner {Service} answered its schema route with a body that is no schema document; the references into it stay not validated", serviceKey);
        else if (reason == "size")
            logger.LogWarning("OxQL owner {Service} answered its schema route with a body of more than {Limit} bytes, which is not read; the references into it stay not validated", serviceKey, SchemaBodyLimit);
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

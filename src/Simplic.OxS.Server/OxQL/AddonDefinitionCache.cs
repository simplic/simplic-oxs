using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OxQL.Core.Models;
using OxQL.Model.Addon;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.ServiceDefinition;

namespace Simplic.OxS.Server.OxQL;

/// <summary>What a call of <see cref="AddonDefinitionCache.GetAsync"/> waited for.</summary>
public enum AddonDefinitionWait
{
    /// <summary>Nothing: the definitions came from memory.</summary>
    None,

    /// <summary>The repository: this call read it.</summary>
    Read,

    /// <summary>The repository: another call was reading it, and this one waited for that read.</summary>
    Joined,
}

/// <summary>The definitions of one entity and organisation, and what the call waited for to have them.</summary>
/// <param name="Definitions">The definitions, retired ones included.</param>
/// <param name="Waited">Whether the call waited for the repository.</param>
public readonly record struct AddonDefinitionLookup(IReadOnlyList<AddonDefinition> Definitions, AddonDefinitionWait Waited);

/// <summary>
/// The in-process cache of an organisation's addon definitions per entity. One instance per host.
/// <para>
/// A request waits for the repository only when the cache holds nothing it may serve: the first
/// read of an entity and organisation in this process, the read after a write here
/// (<see cref="Invalidate"/>), and a value older than it may be served (<see cref="ServedFor"/>:
/// <see cref="StaleFactor"/> lifetimes, and never longer than <see cref="MaxStale"/>). Calls
/// that miss together share one read. A value older than its lifetime
/// (<c>OxQL:Cache:AddonDefinitionTtlSeconds</c>) is served at once and replaced by one read in the
/// background, in a scope of its own, so no request waits for it.
/// </para>
/// <para>
/// What a request may therefore see of a definition written elsewhere: on the instance that wrote
/// it, the write, from the next request on. On another instance, the value it holds until that
/// value is a lifetime old; the first request after that still gets it and starts the read, and
/// requests that arrive once the read is back (a few milliseconds) get the write. A value nobody
/// asked for during <see cref="StaleFactor"/> lifetimes is not served at all: that request waits
/// for the read. So the oldest definition a request can bind with is <see cref="StaleFactor"/>
/// lifetimes old, five minutes at the default lifetime of 30 seconds, and a host that raises the
/// lifetime does not multiply that: past <see cref="MaxStale"/> a value is served only while it is
/// within its lifetime.
/// </para>
/// <para>
/// The cache holds at most <see cref="Capacity"/> entries; beyond that the entries asked for
/// longest ago leave.
/// </para>
/// </summary>
public sealed class AddonDefinitionCache : IDisposable
{
    /// <summary>How many lifetimes old a value may be and still be served while it is read again; an older one is read before it is served.</summary>
    public const int StaleFactor = 10;

    /// <summary>
    /// The longest a value past its lifetime is served while it is read again, whatever the lifetime:
    /// <see cref="StaleFactor"/> lifetimes of the default lifetime. A host that configures a longer
    /// lifetime gets that lifetime and no more staleness on top of it than this allows.
    /// </summary>
    public static readonly TimeSpan MaxStale = TimeSpan.FromMinutes(5);

    /// <summary>The most entries (an entity of an organisation each) the cache holds.</summary>
    public const int Capacity = 4096;

    /// <summary>The longest a failed background read keeps the next one from starting.</summary>
    private static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<(Guid Organisation, string Entity), Entry> entries = new();
    private readonly CancellationTokenSource disposed = new();
    private readonly CancellationToken stopping;
    private readonly OxQLOptions options;
    private readonly IServiceScopeFactory? scopes;
    private readonly TimeProvider time;
    private readonly ILogger<AddonDefinitionCache>? logger;

    /// <summary>
    /// A cache with the configured lifetime. Without <paramref name="scopes"/> nothing can be read
    /// outside a request, so a value older than its lifetime is read by the request that finds it.
    /// </summary>
    /// <param name="options">The engine's options; the lifetime is <c>Cache.AddonDefinitionTtlSeconds</c>, read at every call.</param>
    /// <param name="scopes">Where the background read gets a repository of its own.</param>
    /// <param name="time">The clock; the system's unless a test says otherwise.</param>
    /// <param name="logger">Told when a background read fails.</param>
    public AddonDefinitionCache(OxQLOptions options, IServiceScopeFactory? scopes = null, TimeProvider? time = null, ILogger<AddonDefinitionCache>? logger = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.scopes = scopes;
        this.time = time ?? TimeProvider.System;
        this.logger = logger;
        stopping = disposed.Token;
    }

    /// <summary>The entries held.</summary>
    public int Count => entries.Count;

    private TimeSpan Lifetime => TimeSpan.FromSeconds(Math.Max(1, options.Cache.AddonDefinitionTtlSeconds));

    /// <summary>
    /// How old a value may be and still be served: <see cref="StaleFactor"/> lifetimes, at most
    /// <see cref="MaxStale"/>, and never less than the lifetime itself (within it a value is not stale).
    /// </summary>
    public static TimeSpan ServedFor(TimeSpan lifetime)
    {
        var stale = lifetime * StaleFactor;

        return stale <= MaxStale ? stale : lifetime > MaxStale ? lifetime : MaxStale;
    }

    /// <summary>The cached definitions of one entity and organisation, when the cache holds a value it may serve. Starts no read.</summary>
    public bool TryGet(Guid organisation, string entity, out IReadOnlyList<AddonDefinition> definitions)
    {
        definitions = null!;

        if (!entries.TryGetValue((organisation, entity), out var entry))
            return false;

        lock (entry)
        {
            if (entry.Dropped || entry.Value is null || time.GetElapsedTime(entry.Loaded) > ServedFor(Lifetime))
                return false;

            definitions = entry.Value;

            return true;
        }
    }

    /// <summary>Caches the definitions of one entity and organisation as read now.</summary>
    public void Set(Guid organisation, string entity, IReadOnlyList<AddonDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        while (true)
        {
            var entry = EntryOf((organisation, entity));

            lock (entry)
            {
                if (entry.Dropped)
                    continue;

                entry.Value = definitions;
                entry.Loaded = entry.Used = time.GetTimestamp();

                return;
            }
        }
    }

    /// <summary>
    /// Drops the cached definitions of one entity and organisation; the next call reads the
    /// repository. A read that was under way keeps its answer for the calls already waiting for
    /// it and stores nothing: it may have begun before the write that asked for this.
    /// </summary>
    public void Invalidate(Guid organisation, string entity)
    {
        if (entries.TryRemove((organisation, entity), out var entry))
            lock (entry)
                entry.Dropped = true;
    }

    /// <summary>
    /// The definitions of one entity and organisation: from memory when the cache holds a value it
    /// may serve (starting one background read when that value is older than its lifetime), else
    /// from <paramref name="read"/>, which the calls that miss together run once.
    /// </summary>
    /// <param name="organisation">The organisation.</param>
    /// <param name="entity">The entity's current id.</param>
    /// <param name="read">Reads the repository with the caller's own repository and token.</param>
    /// <param name="cancellationToken">The caller's token: it ends this call's wait, and the read when this call runs it.</param>
    public ValueTask<AddonDefinitionLookup> GetAsync(Guid organisation, string entity, Func<CancellationToken, Task<IReadOnlyList<AddonDefinition>>> read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);

        return entries.TryGetValue((organisation, entity), out var entry) && Held(entry, organisation, entity) is { } held
            ? ValueTask.FromResult(new AddonDefinitionLookup(held, AddonDefinitionWait.None))
            : new ValueTask<AddonDefinitionLookup>(ReadAsync(organisation, entity, read, cancellationToken));
    }

    /// <summary>The background read of an entity's definitions that is under way, or a completed task: what a test waits for.</summary>
    internal Task RefreshedAsync(Guid organisation, string entity)
    {
        if (!entries.TryGetValue((organisation, entity), out var entry))
            return Task.CompletedTask;

        lock (entry)
            return entry.Flight is { } flight ? flight.ContinueWith(static _ => { }, TaskScheduler.Default) : Task.CompletedTask;
    }

    /// <summary>The value the entry may serve now, or null; a value past its lifetime starts its background read.</summary>
    private IReadOnlyList<AddonDefinition>? Held(Entry entry, Guid organisation, string entity)
    {
        lock (entry)
        {
            if (entry.Dropped || entry.Value is not { } value)
                return null;

            var now = time.GetTimestamp();
            var age = time.GetElapsedTime(entry.Loaded, now);
            var lifetime = Lifetime;

            if (age > lifetime)
            {
                // Without a scope of its own nothing reads outside a request: the caller reads.
                if (scopes is null || age > ServedFor(lifetime))
                    return null;

                if (entry.Flight is null && now >= entry.RetryAt)
                    Refresh(entry, organisation, entity);
            }

            entry.Used = now;

            return value;
        }
    }

    private async Task<AddonDefinitionLookup> ReadAsync(Guid organisation, string entity, Func<CancellationToken, Task<IReadOnlyList<AddonDefinition>>> read, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = EntryOf((organisation, entity));
            TaskCompletionSource<IReadOnlyList<AddonDefinition>>? mine = null;
            Task<IReadOnlyList<AddonDefinition>> flight;

            // A value another call stored since this one looked.
            if (Held(entry, organisation, entity) is { } held)
                return new AddonDefinitionLookup(held, AddonDefinitionWait.None);

            lock (entry)
            {
                if (entry.Dropped)
                    continue;

                if (entry.Flight is null)
                {
                    mine = new TaskCompletionSource<IReadOnlyList<AddonDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);
                    entry.Flight = mine.Task;
                }

                flight = entry.Flight;
            }

            if (mine is null)
            {
                try
                {
                    return new AddonDefinitionLookup(await flight.WaitAsync(cancellationToken), AddonDefinitionWait.Joined);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // The call that was reading gave up; this one is still wanted and asks for itself.
                    continue;
                }
            }

            try
            {
                var value = await read(cancellationToken) ?? [];

                Landed(entry, mine, value);

                return new AddonDefinitionLookup(value, AddonDefinitionWait.Read);
            }
            catch (Exception exception)
            {
                Failed((organisation, entity), entry, mine, exception, backOff: false);

                throw;
            }
        }
    }

    /// <summary>Starts the one background read of an entry whose value is past its lifetime. Called under the entry's lock.</summary>
    private void Refresh(Entry entry, Guid organisation, string entity)
    {
        var mine = new TaskCompletionSource<IReadOnlyList<AddonDefinition>>(TaskCreationOptions.RunContinuationsAsynchronously);

        entry.Flight = mine.Task;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopes!.CreateAsyncScope();

                var repository = scope.ServiceProvider.GetRequiredService<IAddonDefinitionRepository>();
                var model = scope.ServiceProvider.GetRequiredService<OxSchemaRegistry>().Model;

                Landed(entry, mine, await AddonDefinitionSource.LoadAsync(repository, model, entity, organisation, stopping));
            }
            catch (Exception exception)
            {
                Failed((organisation, entity), entry, mine, exception, backOff: true);

                if (exception is not OperationCanceledException)
                    logger?.LogWarning(exception, "The addon definitions of {Entity} could not be read again; the ones held are served until they are {ServedFor} old.", entity, ServedFor(Lifetime));
            }
        });
    }

    /// <summary>A read is back: the entry holds its answer, unless it was dropped while the read ran.</summary>
    private void Landed(Entry entry, TaskCompletionSource<IReadOnlyList<AddonDefinition>> flight, IReadOnlyList<AddonDefinition> value)
    {
        lock (entry)
        {
            entry.Flight = null;

            if (!entry.Dropped)
            {
                entry.Value = value;
                entry.Loaded = entry.Used = time.GetTimestamp();
            }
        }

        flight.TrySetResult(value);
    }

    /// <summary>A read failed: the calls waiting for it are told, and an entry that holds nothing leaves.</summary>
    private void Failed((Guid Organisation, string Entity) key, Entry entry, TaskCompletionSource<IReadOnlyList<AddonDefinition>> flight, Exception exception, bool backOff)
    {
        lock (entry)
        {
            entry.Flight = null;

            if (backOff)
            {
                var wait = Lifetime < RetryAfter ? Lifetime : RetryAfter;

                entry.RetryAt = time.GetTimestamp() + (long)(wait.TotalSeconds * time.TimestampFrequency);
            }

            if (entry.Value is null && !entry.Dropped && entries.TryRemove(new KeyValuePair<(Guid, string), Entry>(key, entry)))
                entry.Dropped = true;
        }

        if (exception is OperationCanceledException)
            flight.TrySetCanceled();
        else
            flight.TrySetException(exception);

        // Nobody may be waiting: the failure is the reader's own, not an unobserved one.
        _ = flight.Task.Exception;
    }

    private Entry EntryOf((Guid Organisation, string Entity) key)
    {
        if (entries.TryGetValue(key, out var entry))
            return entry;

        entry = entries.GetOrAdd(key, static (_, now) => new Entry { Used = now }, time.GetTimestamp());

        if (entries.Count > Capacity)
            Trim();

        return entry;
    }

    /// <summary>Lets the entries asked for longest ago leave until a quarter of the capacity is free.</summary>
    private void Trim()
    {
        foreach (var (key, entry) in entries.ToArray().OrderBy(pair => Volatile.Read(ref pair.Value.Used)).Take(Math.Max(1, entries.Count - Capacity * 3 / 4)))
        {
            lock (entry)
            {
                // An entry a call is reading for stays: its callers are about to use it.
                if (entry.Flight is not null && entry.Value is null)
                    continue;

                if (entries.TryRemove(new KeyValuePair<(Guid, string), Entry>(key, entry)))
                    entry.Dropped = true;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed.IsCancellationRequested)
            return;

        disposed.Cancel();
        disposed.Dispose();
    }

    /// <summary>One entity of one organisation. Its members are read and written under its own lock.</summary>
    private sealed class Entry
    {
        /// <summary>The definitions held, or null while the first read is under way.</summary>
        public IReadOnlyList<AddonDefinition>? Value;

        /// <summary>When <see cref="Value"/> was read.</summary>
        public long Loaded;

        /// <summary>When the entry was last asked for.</summary>
        public long Used;

        /// <summary>The read under way, a request's or the background's.</summary>
        public Task<IReadOnlyList<AddonDefinition>>? Flight;

        /// <summary>Before when no further background read starts, after one failed.</summary>
        public long RetryAt;

        /// <summary>The entry left the cache: whoever holds it asks the cache again.</summary>
        public bool Dropped;
    }
}

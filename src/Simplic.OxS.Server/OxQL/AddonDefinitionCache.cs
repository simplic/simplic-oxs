using Microsoft.Extensions.Caching.Memory;
using OxQL.Core.Models;
using OxQL.Model.Addon;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// The in-process cache of an organisation's addon definitions per entity: invalidated on
/// write in the same host, expiring after <c>OxQL:Cache:AddonDefinitionTtlSeconds</c> on the
/// replicas that did not write. One instance per host.
/// </summary>
public sealed class AddonDefinitionCache(OxQLOptions options) : IDisposable
{
    private readonly MemoryCache memory = new(new MemoryCacheOptions());

    /// <summary>The cached definitions of one entity and organisation, when present.</summary>
    public bool TryGet(Guid organisation, string entity, out IReadOnlyList<AddonDefinition> definitions) =>
        memory.TryGetValue(Key(organisation, entity), out definitions!) && definitions is not null;

    /// <summary>Caches the definitions of one entity and organisation for the configured lifetime.</summary>
    public void Set(Guid organisation, string entity, IReadOnlyList<AddonDefinition> definitions) =>
        memory.Set(Key(organisation, entity), definitions, TimeSpan.FromSeconds(Math.Max(1, options.Cache.AddonDefinitionTtlSeconds)));

    /// <summary>Drops the cached definitions of one entity and organisation; the next read goes to the repository.</summary>
    public void Invalidate(Guid organisation, string entity) => memory.Remove(Key(organisation, entity));

    private static string Key(Guid organisation, string entity) => $"{organisation:D}|{entity}";

    /// <inheritdoc/>
    public void Dispose() => memory.Dispose();
}

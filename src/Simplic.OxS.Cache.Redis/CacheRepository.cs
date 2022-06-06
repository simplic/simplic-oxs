using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Simplic.OxS.Cache.Redis
{
    /// <inheritdoc/>
    public class CacheRepository : ICacheRepository
    {
        private readonly IDistributedCache cache;

        /// <summary>
        /// Initialize repository
        /// </summary>
        /// <param name="cache">Key value store</param>
        public CacheRepository(IDistributedCache cache)
        {
            this.cache = cache;
        }

        /// <inheritdoc/>
        public async Task<T?> Get<T>(string type, string keyName, string key)
        {
            var value = await cache.GetStringAsync($"{type}_{keyName}_{key}");

            if (string.IsNullOrWhiteSpace(value))
                return default(T);
            
            return JsonSerializer.Deserialize<T>(value);
        }

        /// <inheritdoc/>
        public async Task Remove(string type, string keyName, string key)
        {
            await cache.RemoveAsync($"{type}_{keyName}_{key}");
        }

        /// <inheritdoc/>
        public async Task Set<T>(string type, string keyName, string key, T obj)
        {
            if (obj == null)
                return;

            var json = JsonSerializer.Serialize(obj);

            await cache.SetStringAsync($"{type}_{keyName}_{key}", json);
        }
    }
}

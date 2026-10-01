namespace Simplic.OxS.Cache.Service
{
    /// <inheritdoc/>
    public class CacheService : ICacheService
    {
        private readonly ICacheRepository dataCacheRepository;

        /// <summary>
        /// Initialize service
        /// </summary>
        /// <param name="cacheRepository">Repository instance</param>
        public CacheService(ICacheRepository dataCacheRepository)
        {
            this.dataCacheRepository = dataCacheRepository;
        }

        /// <inheritdoc/>
        public async Task<T> Get<T>(string type, string keyName, string key, Func<Task<T>> func, CancellationToken ct = default)
        {
            var obj = await dataCacheRepository.Get<T>(type, keyName, key, ct);

            if (obj != null)
                return obj;

            obj = await func();

            if (obj != null)
                await dataCacheRepository.Set<T>(type, keyName, key, obj, ct);

            return obj;
        }

        /// <inheritdoc/>
        public async Task Remove(string type, IDictionary<string, string> keys, CancellationToken ct = default)
        {
            if (keys == null)
                return;

            foreach (var kvp in keys)
                await dataCacheRepository.Remove(type, kvp.Key, kvp.Value, ct);
        }

        /// <inheritdoc/>
        public async Task Set<T>(string type, IDictionary<string, string> keys, T obj, CancellationToken ct = default)
        {
            if (keys == null)
                return;

            foreach (var kvp in keys)
                await dataCacheRepository.Set(type, kvp.Key, kvp.Value, obj, ct);
        }
    }
}

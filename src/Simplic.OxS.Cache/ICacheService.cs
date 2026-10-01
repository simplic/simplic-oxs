namespace Simplic.OxS.Cache
{
    public interface ICacheService
    {
        Task<T> Get<T>(string type, string keyName, string key, Func<Task<T>> func, CancellationToken ct = default);

        Task Set<T>(string type, IDictionary<string, string> ids, T obj, CancellationToken ct = default);

        Task Remove(string type, IDictionary<string, string> ids, CancellationToken ct = default);
    }
}
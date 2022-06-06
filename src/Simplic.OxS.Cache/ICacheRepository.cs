using System.Threading.Tasks;

namespace Simplic.OxS.Cache
{
    /// <summary>
    /// Repository for reading and writing from the cache
    /// </summary>
    public interface ICacheRepository
    {
        /// <summary>
        /// Read data from the cache
        /// </summary>
        /// <typeparam name="T">Data type</typeparam>
        /// <param name="type">Data cache type</param>
        /// <param name="keyName">Key name of the object</param>
        /// <param name="key">Key value</param>
        /// <returns>Cachned object if exists</returns>
        Task<T> Get<T>(string type, string keyName, string key);

        /// <summary>
        /// Write data to the cache
        /// </summary>
        /// <typeparam name="T">Data type</typeparam>
        /// <param name="type">Data cache type</param>
        /// <param name="keyName">Key name of the object</param>
        /// <param name="key">Key value</param>
        /// <param name="obj">Object to cache</param>
        Task Set<T>(string type, string keyName, string key, T obj);

        /// <summary>
        /// Remove object from cache
        /// </summary>
        /// <param name="type">Data cache type</param>
        /// <param name="keyName">Key name of the object</param>
        /// <param name="key">Key value</param>
        Task Remove(string type, string keyName, string key);
    }
}
using MongoDB.Driver;
using Simplic.OxS.Data;
using Simplic.OxS.Data;

namespace Simplic.OxS.Data.MongoDB
{
    /// <summary>
    /// MongoDB context
    /// </summary>
    public interface IMongoContext : IDatabaseContext
    {
        /// <summary>
        /// Gets a mongodb collection
        /// </summary>
        /// <typeparam name="T">Collection type</typeparam>
        /// <param name="name">Collection name</param>
        /// <returns>Collection if exists</returns>
        IMongoCollection<T> GetCollection<T>(string name);

        /// <summary>
        /// Set database configuration by name
        /// </summary>
        void SetConfiguration(string configurationName);
    }
}

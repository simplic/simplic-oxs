using System;
using MongoDB.Driver;

namespace Simplic.OxS.Data.MongoDB
{
    /// <summary>
    /// Transaction class for the mongo db.
    /// </summary>
    internal class MongoTransaction : ITransaction, IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            Session?.Dispose();
        }

        internal IClientSessionHandle Session { get; set; }
    }
}

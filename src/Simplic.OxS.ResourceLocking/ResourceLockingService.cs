using StackExchange.Redis;

namespace Simplic.OxS.ResourceLocking
{
    /// <inheritdoc/>
    public class ResourceLockingService
    {
        private readonly IDatabase database;

        private static readonly TimeSpan expireTime = new TimeSpan(0, 3, 0);

        public ResourceLockingService(IConnectionMultiplexer connection)
        {
            database = connection.GetDatabase();
        }

        /// <inheritdoc/>
        public bool CreateLock(Guid resourceId, Guid userId)
        {
            var key = resourceId.ToString();

            if (database.KeyExists(key))
                return false;

            return database.StringSet(key, userId.ToString(), expireTime);
        }

        /// <inheritdoc/>
        public bool ReleaseLock(Guid resourceId, Guid userId)
        {
            var key = resourceId.ToString();

            if (database.StringGet(key).Equals(userId.ToString()))
                return database.KeyDelete(key);

            return false;
        }

        /// <inheritdoc/>
        public void RefreshLock(Guid resourceId, Guid userId)
        {
            var key = resourceId.ToString();

            if (database.StringGet(key).Equals(userId.ToString()))
                database.KeyExpire(key, expireTime);
        }

        /// <inheritdoc/>
        public bool CheckLocked(Guid resourceId)
        {
            return database.KeyExists(resourceId.ToString());
        }
    }
}
﻿using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    public class ResourceLockingService : IResourceLockingService
    {
        private readonly IDatabase database;

        private static readonly TimeSpan expireTime = new TimeSpan(0, 3, 0);

        public ResourceLockingService(IConnectionMultiplexer connection)
        {
            database = connection.GetDatabase();
        }

        public bool CreateLock(Guid resourceId, Guid userId)
        {
            var key = resourceId.ToString();

            if (database.KeyExists(key))
                return false;

            return database.StringSet(key, userId.ToString(), expireTime);
        }

        public bool ReleaseLock(Guid resourceId, Guid userId)
        {
            var key = resourceId.ToString();

            if (database.StringGet(key).Equals(userId.ToString()))
                return database.KeyDelete(key);

            return false;
        }

        public void RefreshLock(Guid resourceId, Guid userId)
        {
            var key = resourceId.ToString();

            if (database.StringGet(key).Equals(userId.ToString()))
                database.KeyExpire(key, expireTime);
        }

        public bool CheckLocked(Guid resourceId)
        {
            return database.KeyExists(resourceId.ToString());
        }
    }
}

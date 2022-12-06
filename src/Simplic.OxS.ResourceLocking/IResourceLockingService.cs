using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.ResourceLocking
{
    /// <summary>
    /// Service for locking resources from being edited by another user.
    /// </summary>
    public interface IResourceLockingService
    {
        /// <summary>
        /// Creates a lock on a given resource.
        /// </summary>
        /// <param name="resourceId">Resource given by ID</param>
        /// <param name="userId">User given by ID</param>
        /// <returns>Success state</returns>
        bool CreateLock(Guid resourceId, Guid userId);

        /// <summary>
        /// Releases a lock on a given resource.
        /// </summary>
        /// <param name="resourceId">Resource given by ID</param>
        /// <param name="userId">User given by ID</param>
        /// <returns>Success state</returns>
        bool ReleaseLock(Guid resourceId, Guid userId);
        /// <summary>
        /// Resets the lock timeout countdown on a given resource.
        /// </summary>
        /// <param name="resourceId">Resource given by ID</param>
        /// <param name="userId">User given by ID</param>
        void RefreshLock(Guid resourceId, Guid userId);

        /// <summary>
        /// Checks if a given resource is locked.
        /// </summary>
        /// <param name="resourceId">Resource given by ID</param>
        /// <returns>Lock state</returns>
        bool CheckLocked(Guid resourceId);
    }
}

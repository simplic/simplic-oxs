using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    /// <summary>
    /// Service for locking resources stored in the database from being altered by more than one user simultaneously resulting in conflicts.
    /// </summary>
    public interface IResourceLockingService
    {
        /// <summary>
        /// Locks a resource.
        /// </summary>
        /// <param name="resourceId">The resource's ID.</param>
        /// <param name="userId">The acting user's ID.</param>
        /// <returns>Success state</returns>
        bool CreateLock(Guid resourceId, Guid userId);

        /// <summary>
        /// Releases the lock on a resource.
        /// </summary>
        /// <param name="resourceId">The resource's ID.</param>
        /// <param name="userId">The acting user's ID.</param>
        /// <returns>Success state</returns>
        bool ReleaseLock(Guid resourceId, Guid userId);

        /// <summary>
        /// Refreshes the lock resetting the expiry countdown.
        /// </summary>
        /// <param name="resourceId">The resource's ID.</param>
        /// <param name="userId">The acting user's ID.</param>
        void RefreshLock(Guid resourceId, Guid userId);

        /// <summary>
        /// Checks whether a lock exists for a resource.
        /// </summary>
        /// <param name="resourceId">The resource's ID.</param>
        /// <returns>The locked state.</returns>
        bool CheckLocked(Guid resourceId);
    }
}
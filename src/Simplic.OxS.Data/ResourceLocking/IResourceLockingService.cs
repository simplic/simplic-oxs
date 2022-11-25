using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplic.OxS.Data
{
    public interface IResourceLockingService
    {
        bool CreateLock(Guid resourceId, Guid userId);

        bool ReleaseLock(Guid resourceId, Guid userId);

        void RefreshLock(Guid resourceId, Guid userId);

        bool CheckLocked(Guid resourceId);
    }
}

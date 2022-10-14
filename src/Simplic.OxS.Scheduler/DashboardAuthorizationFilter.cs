using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Simplic.OxS.Scheduler
{
    internal class DashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext context)
        {
            return true;
        }
    }
}

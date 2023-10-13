namespace Simplic.OxS.Scheduler
{
    public class JobContextAccessor : IJobContextAccessor
    {
        public JobWithRequestContext? Context { get; set; }
    }
}
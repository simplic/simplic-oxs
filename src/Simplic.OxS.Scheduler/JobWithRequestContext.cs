namespace Simplic.OxS.Scheduler
{
    public class JobWithRequestContext : IJobWithRequestContext
    {
        public IRequestContext? ReqeustContext { get; set; }
    }
}
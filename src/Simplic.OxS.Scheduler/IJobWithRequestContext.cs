namespace Simplic.OxS.Scheduler
{
    public interface IJobWithRequestContext
    {
        public IRequestContext? ReqeustContext { get; set; }
    }
}
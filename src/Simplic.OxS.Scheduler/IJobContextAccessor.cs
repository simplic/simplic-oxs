namespace Simplic.OxS.Scheduler
{
    public interface IJobContextAccessor
    {
        JobWithRequestContext? Context { get; set; }
    }
}
namespace Simplic.OxS
{
    public interface IRequestContext
    {
        public Guid? UserId { get; }
        public Guid? TenantId { get; }
        public Guid? CorrelationId { get; }
    }
}
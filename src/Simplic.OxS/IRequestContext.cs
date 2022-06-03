namespace Simplic.OxS
{
    public interface IRequestContext
    {
        public Guid? UserId { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? CorrelationId { get; set; }
    }
}
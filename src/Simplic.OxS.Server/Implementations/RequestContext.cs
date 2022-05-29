namespace Simplic.OxS.Server.Services
{
    public class RequestContext : IRequestContext
    {
        public Guid? UserId { get; set; }

        public Guid? TenantId { get; set; }

        public Guid? CorrelationId { get; set; }
    }
}

using MassTransit;

namespace Simplic.OxS.MessageBroker.Filter
{
    /// <summary>
    /// Consume filter for injecting <see cref="IRequestContext"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConsumeContextFilter<T> : IFilter<ConsumeContext<T>> where T : class
    {
        private readonly IRequestContext requestContext;

        /// <summary>
        /// Create filter instance
        /// </summary>
        /// <param name="requestContext">Injected request context</param>
        public ConsumeContextFilter(IRequestContext requestContext)
        {
            this.requestContext = requestContext;
        }

        /// <summary>
        /// Inject consume process
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
        {
            requestContext.CorrelationId = context.CorrelationId ?? Guid.NewGuid();

            if (context.TryGetHeader(MassTransitHeaders.UserId, out string? userHeaderId))
                if (Guid.TryParse(userHeaderId, out Guid userId))
                    requestContext.UserId = userId;

            if (context.TryGetHeader(MassTransitHeaders.TenantId, out string? tenantHeaderId))
                if (Guid.TryParse(tenantHeaderId, out Guid tenantId))
                    requestContext.TenantId = tenantId;
        }

        public void Probe(ProbeContext context) { }
    }
}

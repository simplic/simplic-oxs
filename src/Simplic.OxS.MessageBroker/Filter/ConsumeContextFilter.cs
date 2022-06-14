using MassTransit;

namespace Simplic.OxS.MessageBroker.Filter
{
    /// <summary>
    /// Consume filter for injecting <see cref="IRequestContext"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ConsumeContextFilter<T> : IFilter<ConsumeContext<T>> where T : class
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

            if (context.TryGetHeader(MassTransitHeaders.OrganizationId, out string? organizationHeaderId))
                if (Guid.TryParse(organizationHeaderId, out Guid organizationId))
                    requestContext.OrganizationId = organizationId;

            await next.Send(context);
        }

        public void Probe(ProbeContext context) 
        { 

        }
    }
}

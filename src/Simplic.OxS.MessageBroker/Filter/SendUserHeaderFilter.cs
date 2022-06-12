using MassTransit;

namespace Simplic.OxS.MessageBroker.Filter
{
    /// <summary>
    /// Filter for adding masstransit header information when sending a message
    /// </summary>
    /// <typeparam name="T">Message type</typeparam>
    internal class SendUserHeaderFilter<T> : IFilter<SendContext<T>> where T : class
    {
        private readonly IRequestContext requestContext;

        public SendUserHeaderFilter(IRequestContext requestContext)
        {
            this.requestContext = requestContext;
        }

        /// <summary>
        /// Add information to the header
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
        {
            context.Headers.Set(MassTransitHeaders.TenantId, requestContext.TenantId);
            context.Headers.Set(MassTransitHeaders.UserId, requestContext.UserId);

            context.CorrelationId = requestContext.CorrelationId;

            return next.Send(context);
        }

        public void Probe(ProbeContext context)
        {

        }
    }
}

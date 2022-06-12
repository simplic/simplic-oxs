namespace Simplic.OxS.MessageBroker
{
    /// <summary>
    /// Attribute for defining a masstransit consumer.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ConsumerAttribute : Attribute
    {
        /// <summary>
        /// Initialize consumer
        /// </summary>
        public ConsumerAttribute()
        {

        }

        /// <summary>
        /// Initialize consumer
        /// </summary>
        /// <param name="consumerDefinition">Consumer definition</param>
        public ConsumerAttribute(Type consumerDefinition)
        {
            ConsumerDefinition = consumerDefinition;
        }

        /// <summary>
        /// Gets the queue name
        /// </summary>
        public Type? ConsumerDefinition { get; }
    }
}

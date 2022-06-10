namespace Simplic.OxS.MessageBroker
{
    /// <summary>
    /// Attribute for marking a consumer as queueless-consumer.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class NoQueueAttribute : Attribute
    {
        
    }
}

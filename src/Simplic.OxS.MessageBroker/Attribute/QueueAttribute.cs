namespace Simplic.OxS.MessageBroker
{
    /// <summary>
    /// Attribute for defining a masstransit queue.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class QueueAttribute : Attribute
    {
        /// <summary>
        /// Initialize queue attribute
        /// </summary>
        /// <param name="name">Queue name</param>
        public QueueAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"Argument {nameof(name)} must not be null or whitespace.", nameof(name));

            Name = name;
        }

        /// <summary>
        /// Gets the queue name
        /// </summary>
        public string Name { get; }
    }
}

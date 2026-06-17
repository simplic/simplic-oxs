namespace Simplic.OxS.SchemaRegistry
{
    /// <summary>
    /// Event which is published when data is updated in the schema registry.
    /// </summary>
    public interface DataUpdatedEvent
    {
        /// <summary>
        /// The data which has been updated in the schema registry.
        /// </summary>
        public Data Data { get; set; }
    }
}

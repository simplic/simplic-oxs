namespace Simplic.OxS.SchemaRegistry
{
    /// <summary>
    /// Event which is published when new data is created in the schema registry.
    /// </summary>
    public interface DataCreatedEvent
    {
        /// <summary>
        /// The data which has been created in the schema registry.
        /// </summary>
        public Data Data { get; set; }
    }
}

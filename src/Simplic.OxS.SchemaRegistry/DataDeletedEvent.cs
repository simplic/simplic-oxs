namespace Simplic.OxS.SchemaRegistry
{
    /// <summary>
    /// Event which is published when data is deleted in the schema registry.
    /// </summary>
    public interface DataDeletedEvent
    {
        /// <summary>
        /// The data which has been deleted in the schema registry.
        /// </summary>
        public Data Data { get; set; }
    }
}

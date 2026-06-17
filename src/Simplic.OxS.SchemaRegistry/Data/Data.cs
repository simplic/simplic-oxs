using System;

namespace Simplic.OxS.SchemaRegistry
{
    /// <summary>
    /// Represents data in the schema registry.
    /// </summary>
    public interface Data
    {
        /// <summary>
        /// Gets or sets the id of the data which is updated in the schema registry.
        /// </summary>
        public Guid DataId { get; set; }

        /// <summary>
        /// Gets or sets the name of the data which is updated in the schema registry.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the json data which is updated in the schema registry.
        /// </summary>
        public string JsonData { get; set; }
    }
}

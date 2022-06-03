namespace Simplic.OxS.Data.MongoDB
{
    /// <summary>
    /// Connection string settings
    /// </summary>
    public class ConnectionSettings
    {
        /// <summary>
        /// Gets or sets the mongodb connection string
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the mongodb database name
        /// </summary>
        public string Database { get; set; }
    }
}

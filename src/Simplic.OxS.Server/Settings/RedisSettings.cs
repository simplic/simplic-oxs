namespace Simplic.OxS.Server.Settings
{
    /// <summary>
    /// Redis settings
    /// </summary>
    public class RedisSettings
    {
        /// <summary>
        /// Gets or sets the redis connection string
        /// </summary>
        public string RedisCacheUrl { get; set; } = $"";
    }
}

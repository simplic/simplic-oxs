namespace Simplic.OxS.Settings
{
    /// <summary>
    /// Authentication settings
    /// </summary>
    public class AuthSettings
    {
        /// <summary>
        /// Gets or sets the auth token
        /// </summary>
        public string Token { get; set; } = $"{Guid.NewGuid()}";

        /// <summary>
        /// Gets or sets the default issuer
        /// </summary>
        public string Issuer { get; set; } = "Simplic.OxS";

        /// <summary>
        /// Gets or sets the internal API key
        /// </summary>
        public string InternalApiKey { get; set; } = $"{Guid.NewGuid()}";
    }
}
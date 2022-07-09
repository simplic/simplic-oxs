namespace Simplic.OxS
{
    /// <summary>
    /// Service constants
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Internal api key scheme (i-api-key).
        /// </summary>
        public const string HttpAuthorizationSchemeInternalKey = "i-api-key";
    
        /// <summary>
        /// Bearer scheme (bearer).
        /// </summary>
        public const string HttpAuthorizationSchemeBearerKey = "bearer";

        /// <summary>
        /// Gets or sets the name/key of the http-header for the user id
        /// </summary>
        public const string HttpHeaderUserIdKey = "UserId";

        /// <summary>
        /// Gets or sets the name/key of the http-header for the organization id
        /// </summary>
        public const string HttpHeaderOrganizationIdKey = "OrganizationId";

        /// <summary>
        /// Gets or sets the name/key of the http-header for the correlation id
        /// </summary>
        public const string HttpHeaderCorrelationIdKey = "X-Correlation-ID";
    }
}

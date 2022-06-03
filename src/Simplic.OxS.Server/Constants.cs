﻿namespace Simplic.OxS.Server
{
    /// <summary>
    /// Service constants
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Internal api key scheme (i-api-key).
        /// </summary>
        public const string InternalApiKeyAuth = "i-api-key";

        /// <summary>
        /// Gets or sets the name/key of the http-header for the user id
        /// </summary>
        public const string HttpHeaderUserIdKey = "UserId";

        /// <summary>
        /// Gets or sets the name/key of the http-header for the tenant id
        /// </summary>
        public const string HttpHeaderTenantIdKey = "TenantId";

        /// <summary>
        /// Gets or sets the name/key of the http-header for the correlation id
        /// </summary>
        public const string HttpHeaderCorrelationIdKey = "X-Correlation-ID";
    }
}

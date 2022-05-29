using Microsoft.AspNetCore.Mvc;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Represents the simplic oxs base controller
    /// </summary>
    [AuthorizeInternalApiKey]
    public abstract class OxSInternalController : ControllerBase
    {
        /// <summary>
        /// Gets the actual user id from the http request header
        /// </summary>
        public Guid? UserId { get; internal set; }

        /// <summary>
        /// Gets the actual tenant id from the http request header
        /// </summary>
        public Guid? TenantId { get; internal set; }
    }
}

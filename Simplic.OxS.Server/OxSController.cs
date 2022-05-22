namespace Simplic.OxS.Server
{
    /// <summary>
    /// Represents the simplic oxs base controller
    /// </summary>
    public abstract class OxSController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        /// <summary>
        /// Gets the actual user id from the given jwt token
        /// </summary>
        /// <returns>User id as guid. Null if no user id was found.</returns>
        protected Guid? GetUserId()
        {
            var claim = User.Claims.FirstOrDefault(x => x.Type == "Id");

            if (claim == null)
                return null;

            return Guid.Parse(claim.Value);
        }

        /// <summary>
        /// Gets the actual tenant id from the given jwt token
        /// </summary>
        /// <returns>Tenant id as guid. Null if no tenant id was found.</returns>
        protected Guid? GetTenantId()
        {
            var claim = User.Claims.FirstOrDefault(x => x.Type == "TId");

            if (claim == null)
                return null;

            return Guid.Parse(claim.Value);
        }
    }
}

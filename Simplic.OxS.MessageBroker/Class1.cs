using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Simplic.OxS.MessageBroker
{

    public static class ClaimPrincipalExtensions
    {
        public static List<T> GetRoles<T>(this ClaimsPrincipal user, string rolesType) where T : struct, Enum
        {
            var roles = user.FindFirstValue(rolesType);

            return string.IsNullOrWhiteSpace(roles)
                ? new List<T>()
                : roles.Split(",").Select(r => Enum.Parse<T>(r)).ToList();
        }

        public static string? OrganizationId(this ClaimsPrincipal principal)
        {
            return principal?.FindFirstValue(Constants.ClaimTypes.OrganizationId);
        }

        public static string? Id(this ClaimsPrincipal principal)
        {
            return principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}

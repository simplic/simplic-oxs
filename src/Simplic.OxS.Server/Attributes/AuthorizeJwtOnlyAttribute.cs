using Microsoft.AspNetCore.Authorization;

namespace Simplic.OxS.Server;

/// <summary>
/// Attribute for securing controllers or methods from getting called with api keys for authenticaiton.
/// </summary>
public class AuthorizeJwtOnlyAttribute() : AuthorizeAttribute(policy: "JwtOnly")
{
}

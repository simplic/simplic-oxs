using Microsoft.AspNetCore.Authorization;

namespace Simplic.OxS.Server.Extensions;

public class ApiKeyOrJwtAuthorizeAttribute : AuthorizeAttribute
{
    public ApiKeyOrJwtAuthorizeAttribute() : base(policy: "ApiKeyOrJwt")
    {
        // This is just a marker to indicate that both JWT and API Key authentication are acceptable.
    }
}

using Microsoft.AspNetCore.Mvc;

namespace Simplic.OxS.Server.Controller
{
    /// <summary>
    /// Represents the simplic oxs base controller
    /// </summary>
    [AuthorizeInternalApiKey]
    public abstract class OxSInternalController : ControllerBase
    {

    }
}

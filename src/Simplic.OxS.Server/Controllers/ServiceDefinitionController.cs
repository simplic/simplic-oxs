using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.ServiceDefinition;
using System.Net;

namespace Simplic.OxS.Server.Controllers;

/// <summary>
/// Controller for managing service definition
/// </summary>
[Authorize]
[ApiController]
[Route("[Controller]")]
public class ServiceDefinitionController : OxSController
{
    /// <summary>
    /// Get all organization settings
    /// </summary>
    /// <returns>Collection of organization settings</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ServiceObject), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> GetServiceDefinition()
    {
        return Ok();
    }

    /// <summary>
    /// Registers the service contract for the current organization
    /// </summary>
    [HttpPost("register-service")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> RegisterService()
    {
        return Ok();
    }

    /// <summary>
    /// Registers the service contract for the current organization
    /// </summary>
    [HttpPost("set")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> RegisterContractService()
    {
        return Ok();
    }
}
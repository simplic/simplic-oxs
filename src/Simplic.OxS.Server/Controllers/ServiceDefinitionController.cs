using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Service;
using Simplic.OxS.ServiceDefinition;
using System.Net;
using System.Reflection;

namespace Simplic.OxS.Server.Controllers;

/// <summary>
/// Controller for managing service definition
/// </summary>
[Authorize]
[ApiController]
[Route("[Controller]")]
public class ServiceDefinitionController : OxSController
{
    private ServiceDefinitionService serviceDefinitionService;

    /// <summary>
    /// Create controller instance
    /// </summary>
    /// <param name="serviceDefinitionService">Service definition service</param>
    public ServiceDefinitionController(ServiceDefinitionService serviceDefinitionService)
    {
        this.serviceDefinitionService = serviceDefinitionService;
    }

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
        return Ok(serviceDefinitionService.ServiceObject);
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
}
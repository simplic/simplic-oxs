using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Settings.Organization;
using Simplic.OxS.Settings.Organization.Dto;
using Simplic.OxS.Settings.Organization.Exceptions;
using System.Net;
using Microsoft.AspNetCore.Authorization;

namespace Simplic.OxS.Server.Controllers;

/// <summary>
/// Controller for managing organization settings
/// </summary>
[Authorize]
[ApiController]
[Route("[Controller]")]
public class OrganizationSettingsController : OxSController
{
    private readonly IOrganizationSettingsProvider settingsProvider;
    private readonly IRequestContext requestContext;

    /// <summary>
    /// Initialize controller
    /// </summary>
    /// <param name="settingsProvider">Settings provider service</param>
    /// <param name="requestContext">Request context</param>
    public OrganizationSettingsController(
        IOrganizationSettingsProvider settingsProvider,
        IRequestContext requestContext)
    {
        this.settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        this.requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
    }

    /// <summary>
    /// Get all organization settings
    /// </summary>
    /// <returns>Collection of organization settings</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<OrganizationSettingResult>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<OrganizationSettingResult>>> GetAllSettings()
    {
        if (!requestContext.OrganizationId.HasValue)
            return BadRequest("Organization context is required");

        var settings = await settingsProvider.GetAllAsync();
        return Ok(settings);
    }

    /// <summary>
    /// Get a specific organization setting by internal name
    /// </summary>
    /// <param name="internalName">Internal name of the setting</param>
    /// <returns>Organization setting</returns>
    [HttpGet("{internalName}")]
    [ProducesResponseType(typeof(OrganizationSettingResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<OrganizationSettingResult>> GetSetting(string internalName)
    {
        if (string.IsNullOrWhiteSpace(internalName))
            return BadRequest("Internal name is required");

        try
        {
            var setting = await settingsProvider.GetAsync(internalName);
            return Ok(setting);
        }
        catch (SettingNotFoundException ex)
        {
            return NotFound($"Setting '{ex.InternalName}' not found");
        }
    }

    /// <summary>
    /// Update a specific organization setting
    /// </summary>
    /// <param name="internalName">Internal name of the setting</param>
    /// <param name="request">Update request</param>
    /// <returns>No content if successful</returns>
    [HttpPut("{internalName}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> UpdateSetting(
        string internalName,
        [FromBody] UpdateSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(internalName))
            return BadRequest("Internal name is required");

        if (request?.Value == null)
            return BadRequest("Value is required");

        try
        {
            await settingsProvider.SetAsync(
                internalName,
                request.Value);

            return NoContent();
        }
        catch (SettingNotFoundException ex)
        {
            return NotFound();
        }
        catch (SettingTypeMismatchException ex)
        {
            return BadRequest($"Invalid value type for setting '{ex.InternalName}'. Expected '{ex.ExpectedType.Name}' but got '{ex.ActualType.Name}'");
        }
        catch (SettingValueNullException ex)
        {
            return BadRequest($"Setting '{ex.InternalName}' cannot be null");
        }
    }
}

/// <summary>
/// Request model for updating a setting value
/// </summary>
/// <param name="Value">The new value for the setting</param>
public record UpdateSettingRequest(object Value);
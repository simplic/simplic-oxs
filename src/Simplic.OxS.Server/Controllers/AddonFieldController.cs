using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Controllers.Model;
using Simplic.OxS.ServiceDefinition;
using System.Net;

namespace Simplic.OxS.Server.Controllers;

/// <summary>
/// Controller for managing addon field configurations per organization.
/// </summary>
[Authorize]
[ApiController]
[Route("[Controller]")]
public class AddonFieldController : OxSController
{
    private readonly IAddonFieldRepository addonFieldRepository;
    private readonly IRequestContext requestContext;

    /// <summary>
    /// Initializes a new instance of <see cref="AddonFieldController"/>.
    /// </summary>
    public AddonFieldController(IAddonFieldRepository addonFieldRepository, IRequestContext requestContext)
    {
        this.addonFieldRepository = addonFieldRepository;
        this.requestContext = requestContext;
    }

    /// <summary>
    /// Gets all addon fields for the current organization.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AddonFieldResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var fields = await addonFieldRepository.GetAllAsync();
        return Ok(fields.Select(MapToResponse));
    }

    /// <summary>
    /// Gets all addon fields for a specific object name.
    /// </summary>
    /// <param name="objectName">The object name (e.g. "logistics.shipment").</param>
    [HttpGet("{objectName}")]
    [ProducesResponseType(typeof(IEnumerable<AddonFieldResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> GetByObjectName(string objectName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return BadRequest("Object name is required.");

        var fields = await addonFieldRepository.GetByObjectNameAsync(objectName);
        return Ok(fields.Select(MapToResponse));
    }

    /// <summary>
    /// Gets a single addon field by its id.
    /// </summary>
    /// <param name="id">The addon field id.</param>
    [HttpGet("by-id/{id:guid}")]
    [ProducesResponseType(typeof(AddonFieldResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var field = await addonFieldRepository.GetAsync(id);
        if (field == null || field.IsDeleted)
            return NotFound();

        return Ok(MapToResponse(field));
    }

    /// <summary>
    /// Creates a new addon field for the current organization.
    /// </summary>
    /// <param name="request">The create request.</param>
    [HttpPost]
    [ProducesResponseType(typeof(AddonFieldResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateAddonFieldRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        if (string.IsNullOrWhiteSpace(request.ObjectName))
            return BadRequest("Object name is required.");

        if (string.IsNullOrWhiteSpace(request.PropertyName))
            return BadRequest("Property name is required.");

        if (string.IsNullOrWhiteSpace(request.PropertyType))
            return BadRequest("Property type is required.");

        var field = new AddonField
        {
            Id = Guid.NewGuid(),
            OrganizationId = requestContext.OrganizationId!.Value,
            ObjectName = request.ObjectName,
            PropertyName = request.PropertyName,
            PropertyType = request.PropertyType,
            Description = request.Description,
            IsDeleted = false
        };

        await addonFieldRepository.CreateAsync(field);
        await addonFieldRepository.CommitAsync();

        return Ok(MapToResponse(field));
    }

    /// <summary>
    /// Updates an existing addon field.
    /// </summary>
    /// <param name="id">The addon field id.</param>
    /// <param name="request">The update request.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AddonFieldResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAddonFieldRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        if (string.IsNullOrWhiteSpace(request.PropertyName))
            return BadRequest("Property name is required.");

        if (string.IsNullOrWhiteSpace(request.PropertyType))
            return BadRequest("Property type is required.");

        var field = await addonFieldRepository.GetAsync(id);
        if (field == null || field.IsDeleted)
            return NotFound();

        if (field.OrganizationId != requestContext.OrganizationId!.Value)
            return NotFound();

        field.PropertyName = request.PropertyName;
        field.PropertyType = request.PropertyType;
        field.Description = request.Description;

        await addonFieldRepository.UpdateAsync(field);
        await addonFieldRepository.CommitAsync();

        return Ok(MapToResponse(field));
    }

    /// <summary>
    /// Deletes an addon field by its id (soft delete).
    /// </summary>
    /// <param name="id">The addon field id.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var field = await addonFieldRepository.GetAsync(id);
        if (field == null || field.IsDeleted)
            return NotFound();

        if (field.OrganizationId != requestContext.OrganizationId!.Value)
            return NotFound();

        field.IsDeleted = true;

        await addonFieldRepository.UpdateAsync(field);
        await addonFieldRepository.CommitAsync();

        return NoContent();
    }

    private static AddonFieldResponse MapToResponse(AddonField field) => new()
    {
        Id = field.Id,
        ObjectName = field.ObjectName,
        PropertyName = field.PropertyName,
        PropertyType = field.PropertyType,
        Description = field.Description
    };
}

using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Settings.Abstractions;
using Simplic.OxS.Settings.Organization;
using Simplic.OxS.Settings.Organization.Exceptions;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

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
    private readonly OrganizationSettingsRegistry registry;

    /// <summary>
    /// Initialize controller
    /// </summary>
    /// <param name="settingsProvider">Settings provider service</param>
    /// <param name="requestContext">Request context</param>
    /// <param name="registry">Settings registry</param>
    public OrganizationSettingsController(
        IOrganizationSettingsProvider settingsProvider,
        IRequestContext requestContext,
        OrganizationSettingsRegistry registry)
    {
        this.settingsProvider = settingsProvider;
        this.requestContext = requestContext;
        this.registry = registry;
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

        if (!requestContext.OrganizationId.HasValue || requestContext.OrganizationId == Guid.Empty)
            return BadRequest("Organization context is required");

        try
        {
            // Get the setting definition to know the expected type
            if (!registry.TryGet(internalName, out var definition) || definition == null)
                return NotFound($"Setting '{internalName}' not found");

            // Convert JsonElement to the proper type if needed
            var convertedValue = ConvertValueToExpectedType(request.Value, definition.ValueType);

            await settingsProvider.SetAsync(internalName, convertedValue);

            return NoContent();
        }
        catch (SettingNotFoundException)
        {
            return NotFound($"Setting '{internalName}' not found");
        }
        catch (SettingTypeMismatchException ex)
        {
            return BadRequest($"Invalid value type for setting '{ex.InternalName}'. Expected '{ex.ExpectedType.Name}' but got '{ex.ActualType.Name}'");
        }
        catch (SettingValueNullException ex)
        {
            return BadRequest($"Setting '{ex.InternalName}' cannot be null");
        }
        catch (JsonException ex)
        {
            return BadRequest($"Invalid JSON value for setting '{internalName}': {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest($"Invalid value for setting '{internalName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Convert the received value to the expected type
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <param name="expectedType">The expected type</param>
    /// <returns>Converted value</returns>
    private object? ConvertValueToExpectedType(object value, Type expectedType)
    {
        // If it's already the correct type, return as-is
        if (value?.GetType() == expectedType)
            return value;

        // Handle JsonElement conversion
        if (value is JsonElement jsonElement)
        {
            // Handle null values
            if (jsonElement.ValueKind == JsonValueKind.Null)
                return null;

            // Handle nullable types
            var actualType = Nullable.GetUnderlyingType(expectedType) ?? expectedType;

            return actualType switch
            {
                Type t when t == typeof(bool) => jsonElement.GetBoolean(),
                Type t when t == typeof(int) => jsonElement.GetInt32(),
                Type t when t == typeof(long) => jsonElement.GetInt64(),
                Type t when t == typeof(decimal) => jsonElement.GetDecimal(),
                Type t when t == typeof(double) => jsonElement.GetDouble(),
                Type t when t == typeof(float) => jsonElement.GetSingle(),
                Type t when t == typeof(string) => jsonElement.GetString(),
                Type t when t == typeof(Guid) => jsonElement.GetGuid(),
                Type t when t == typeof(DateTime) => jsonElement.GetDateTime(),
                Type t when t == typeof(DateOnly) => DateOnly.FromDateTime(jsonElement.GetDateTime()),
                Type t when t == typeof(TimeOnly) => TimeOnly.FromTimeSpan(jsonElement.GetDateTime().TimeOfDay),
                Type t when t.IsEnum => Enum.Parse(actualType, jsonElement.GetString() ?? string.Empty),
                _ => JsonSerializer.Deserialize(jsonElement.GetRawText(), expectedType)
            };
        }

        // Try direct conversion for other types
        try
        {
            return Convert.ChangeType(value, expectedType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot convert value of type '{value?.GetType().Name}' to expected type '{expectedType.Name}'", ex);
        }
    }
}

/// <summary>
/// Request model for updating a setting value
/// </summary>
/// <param name="Value">The new value for the setting</param>
public record UpdateSettingRequest(object Value);
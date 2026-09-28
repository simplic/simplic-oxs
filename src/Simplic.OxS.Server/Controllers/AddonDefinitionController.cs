using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Controllers.Model;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.ServiceDefinition;
using System.Net;

namespace Simplic.OxS.Server.Controllers;

/// <summary>
/// Manages an organisation's addon definitions: the keys it declares under an extendable
/// entity's addon bag, each a hint about the kind the query engine filters and sorts the key
/// as. Definitions are per organisation and owned by the service that stores the values.
/// A definition's path and kind are immutable; labels and the value list may change; a
/// retired definition stays as a row and its key is opaque again. An entity's definitions are
/// the rows under its current id and under every id it retired; the API answers with the
/// current id and moves a row it writes to it.
/// </summary>
[Authorize]
[ApiController]
[Route("[Controller]")]
public class AddonDefinitionController : OxSController
{
    private readonly IAddonDefinitionRepository repository;
    private readonly IRequestContext requestContext;
    private readonly OxSchemaRegistry schema;
    private readonly AddonDefinitionCache cache;

    /// <summary>
    /// Initializes a new instance of <see cref="AddonDefinitionController"/>.
    /// </summary>
    public AddonDefinitionController(IAddonDefinitionRepository repository, IRequestContext requestContext, OxSchemaRegistry schema, AddonDefinitionCache cache)
    {
        this.repository = repository;
        this.requestContext = requestContext;
        this.schema = schema;
        this.cache = cache;
    }

    /// <summary>
    /// Gets every definition of one entity for the current organisation, retired ones flagged,
    /// including the rows stored under an id the entity retired.
    /// </summary>
    /// <param name="entity">The entity id (e.g. "logistics.shipment"), current or retired.</param>
    /// <param name="ct">The request's cancellation token.</param>
    [HttpGet("{entity}")]
    [ProducesResponseType(typeof(IEnumerable<AddonDefinitionResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> GetByEntityAsync(string entity, CancellationToken ct)
    {
        if (!schema.Model.TryResolve(entity, out _, out _))
            return NotFound();

        var definitions = await AddonDefinitionSource.ReadAsync(repository, schema.Model, entity, organisation: null);

        return Ok(definitions.OrderBy(definition => definition.Path, StringComparer.Ordinal).Select(MapToResponse));
    }

    /// <summary>
    /// Gets a single definition by its id.
    /// </summary>
    /// <param name="id">The definition id.</param>
    /// <param name="ct">The request's cancellation token.</param>
    [HttpGet("by-id/{id:guid}")]
    [ActionName(nameof(GetByIdAsync))] // the route name Create points at; MVC would otherwise strip the Async suffix
    [ProducesResponseType(typeof(AddonDefinitionResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var definition = await FindAsync(id);

        return definition is null ? NotFound() : Ok(MapToResponse(definition));
    }

    /// <summary>
    /// Creates a definition for the current organisation. Refused for an unknown or
    /// non-extendable entity, a malformed path, an unknown kind, a value list the kind does
    /// not allow, and a path that a defined scalar shadows or that would shadow one. A retired
    /// definition of the same path and kind, also one stored under a retired entity id, is
    /// revived instead of duplicated. The definition is stored under the entity's current id.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <param name="ct">The request's cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(AddonDefinitionResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.Conflict)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateAddonDefinitionRequest request, CancellationToken ct)
    {
        if (requestContext.OrganizationId is not { } organizationId)
            return Forbid();

        var entity = AddonDefinitionSource.CurrentId(schema.Model, request.Entity);

        if (AddonDefinitionRules.CheckEntity(schema.Model, entity) is { } entityError)
            return BadRequest(entityError);

        if (AddonDefinitionRules.CheckPath(request.Path) is { } pathError)
            return BadRequest(pathError);

        if (AddonDefinitionRules.CheckKind(request.Kind) is { } kindError)
            return BadRequest(kindError);

        var kind = AddonDefinitionRules.ParseKind(request.Kind)!.Value;
        var values = MapValues(request.Values);

        if (AddonDefinitionRules.CheckValues(kind, values) is { } valuesError)
            return BadRequest(valuesError);

        var existing = (await AddonDefinitionSource.ReadAsync(repository, schema.Model, entity, organisation: null)).ToList();
        var same = existing.FirstOrDefault(definition => string.Equals(definition.Path, request.Path, StringComparison.Ordinal));

        if (same is { Retired: false })
            return Conflict($"'{request.Path}' is already defined on '{entity}'.");

        if (same is not null && !string.Equals(same.Kind, request.Kind, StringComparison.Ordinal))
            return Conflict($"'{request.Path}' was defined as {same.Kind} and retired; a definition's kind is immutable.");

        if (AddonDefinitionRules.CheckShadowing(request.Path, kind, existing) is { } shadowError)
            return BadRequest(shadowError);

        AddonDefinitionDocument definition;

        if (same is not null)
        {
            // Revive the retired row rather than store a second definition of one path.
            definition = same;
            definition.Entity = entity;
            definition.Retired = false;
            definition.Values = values;
            definition.DisplayName = request.DisplayName;
            definition.Description = request.Description;

            await repository.UpdateAsync(definition);
        }
        else
        {
            definition = new AddonDefinitionDocument
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Entity = entity,
                Path = request.Path,
                Kind = request.Kind,
                Values = values,
                DisplayName = request.DisplayName,
                Description = request.Description,
                Retired = false,
                IsDeleted = false,
            };

            await repository.CreateAsync(definition);
        }

        await repository.CommitAsync();
        cache.Invalidate(organizationId, entity);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = definition.Id }, MapToResponse(definition));
    }

    /// <summary>
    /// Updates the labels and the value list of a definition; path and kind never change.
    /// </summary>
    /// <param name="id">The definition id.</param>
    /// <param name="request">The update request.</param>
    /// <param name="ct">The request's cancellation token.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AddonDefinitionResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateAddonDefinitionRequest request, CancellationToken ct)
    {
        var definition = await FindAsync(id);

        if (definition is null)
            return NotFound();

        var kind = AddonDefinitionRules.ParseKind(definition.Kind) ?? global::OxQL.Model.Addon.AddonKind.Object;
        var values = MapValues(request.Values);

        if (AddonDefinitionRules.CheckValues(kind, values) is { } valuesError)
            return BadRequest(valuesError);

        definition.Entity = AddonDefinitionSource.CurrentId(schema.Model, definition.Entity);
        definition.Values = values;
        definition.DisplayName = request.DisplayName;
        definition.Description = request.Description;

        await repository.UpdateAsync(definition);
        await repository.CommitAsync();
        cache.Invalidate(definition.OrganizationId, definition.Entity);

        return Ok(MapToResponse(definition));
    }

    /// <summary>
    /// Retires a definition: the row stays, the key is opaque again.
    /// </summary>
    /// <param name="id">The definition id.</param>
    /// <param name="ct">The request's cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        var definition = await FindAsync(id);

        if (definition is null)
            return NotFound();

        if (!definition.Retired)
        {
            definition.Entity = AddonDefinitionSource.CurrentId(schema.Model, definition.Entity);
            definition.Retired = true;

            await repository.UpdateAsync(definition);
            await repository.CommitAsync();
            cache.Invalidate(definition.OrganizationId, definition.Entity);
        }

        return NoContent();
    }

    /// <summary>The definition with the given id in the current organisation, or null.</summary>
    private async Task<AddonDefinitionDocument?> FindAsync(Guid id)
    {
        var definition = await repository.GetAsync(id);

        if (definition is null || definition.IsDeleted)
            return null;

        return definition.OrganizationId == requestContext.OrganizationId ? definition : null;
    }

    private static List<AddonDefinitionValue>? MapValues(List<AddonDefinitionValueRequest>? values) =>
        values is { Count: > 0 }
            ? [.. values.Select(value => new AddonDefinitionValue { Value = value.Value, Label = value.Label })]
            : null;

    /// <summary>The response of one definition, under its entity's current id.</summary>
    private AddonDefinitionResponse MapToResponse(AddonDefinitionDocument definition) => new()
    {
        Id = definition.Id,
        Entity = AddonDefinitionSource.CurrentId(schema.Model, definition.Entity),
        Path = definition.Path,
        Kind = definition.Kind,
        Values = definition.Values is { Count: > 0 } values
            ? [.. values.Select(value => new AddonDefinitionValueResponse { Value = value.Value, Label = value.Label })]
            : null,
        DisplayName = definition.DisplayName,
        Description = definition.Description,
        Retired = definition.Retired,
    };
}

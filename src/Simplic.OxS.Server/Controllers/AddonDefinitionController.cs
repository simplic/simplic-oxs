using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Simplic.OxS.Server.Controller;
using Simplic.OxS.Server.Controllers.Model;
using Simplic.OxS.Server.Extensions;
using Simplic.OxS.Server.OxQL;
using Simplic.OxS.Server.OxSchema;
using Simplic.OxS.ServiceDefinition;
using System.ComponentModel;

namespace Simplic.OxS.Server.Controllers;

/// <summary>
/// Manages an organisation's addon definitions: the keys it declares under an extendable
/// entity's addon bag, each a hint about the kind the query engine filters and sorts the key
/// as. Definitions are per organisation and owned by the service that stores the values.
/// A definition's path and kind are immutable; labels and the value list may change; a
/// retired definition stays as a row and its key is opaque again. An entity's definitions are
/// the rows under its current id and under every id it retired; the API answers with the
/// current id and moves a row it writes to it, unless the current id already holds that path.
/// The store keeps one row per organisation, entity id and path; a write it refuses is a 409.
/// <para>
/// What the OpenAPI document shows of a route is what its attributes say: the summary, the
/// description, the parameters and every status it answers (<see cref="DescribedRouteAttribute"/>).
/// A refusal by a rule of this controller is answered as a sentence (<c>BadRequest</c> and
/// <c>Conflict</c> with a string), and the attributes say so.
/// </para>
/// </summary>
[Authorize]
[ApiController]
[Route("[Controller]")]
public class AddonDefinitionController : OxSController
{
    private const string Json = "application/json";

    private const string Text = "text/plain";

    private const string ProblemJson = "application/problem+json";

    /// <summary>What every description says an addon definition is, and where the rules are.</summary>
    private const string What =
        "An addon definition is a property an organisation adds to an extendable entity of this service: it is one of the properties of the entity's `addon` member "
        + "(`GET /schema/addons`), and OxQL filters and sorts by it as its kind. The rules are described in " + SchemaController.Documentation + ".";

    private const string NoCredentials = "The request carries no credentials.";

    private const string NoSuchDefinition = "The caller's organisation has no definition of that id.";

    private const string DefinedMeanwhile = "The definition was to move to the current id of its entity, and another request defined its path there meanwhile. The body is the reason as a sentence; the request can be sent again.";

    private const string Unreadable = " A body that cannot be read is answered as `application/problem+json`.";

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
    [EndpointSummary("The addon definitions of one entity.")]
    [EndpointDescription("Answers every definition the caller's organisation holds for the entity, ordered by path, the retired ones among them and flagged. " + What)]
    [DescribedRoute(AnswerExample = DescribedRouteExamples.AddonDefinitions)]
    [ProducesResponseType(typeof(IEnumerable<AddonDefinitionResponse>), StatusCodes.Status200OK, Json, Description = "The definitions; an empty list for an entity without any.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, ProblemJson, Description = NoCredentials)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, ProblemJson, Description = "The service has no entity of that id.")]
    public async Task<IActionResult> GetByEntityAsync(
        [Description("The id of the entity, as the schema document names it (`parcel.parcel`). An id the entity had before is read as the entity.")] string entity,
        CancellationToken ct)
    {
        if (!schema.Model.TryResolve(entity, out _, out _))
            return NotFound();

        var definitions = await AddonDefinitionSource.ReadAsync(repository, schema.Model, entity, organisation: null, ct);

        return Ok(definitions.OrderBy(definition => definition.Path, StringComparer.Ordinal).Select(MapToResponse));
    }

    /// <summary>
    /// Gets a single definition by its id.
    /// </summary>
    /// <param name="id">The definition id.</param>
    /// <param name="ct">The request's cancellation token.</param>
    [HttpGet("by-id/{id:guid}")]
    [ActionName(nameof(GetByIdAsync))] // the route name Create points at; MVC would otherwise strip the Async suffix
    [EndpointSummary("One addon definition.")]
    [EndpointDescription("Answers the definition of that id, also a retired one. " + What)]
    [DescribedRoute(AnswerExample = DescribedRouteExamples.AddonDefinition)]
    [ProducesResponseType(typeof(AddonDefinitionResponse), StatusCodes.Status200OK, Json, Description = "The definition.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, ProblemJson, Description = NoCredentials)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, ProblemJson, Description = NoSuchDefinition)]
    public async Task<IActionResult> GetByIdAsync([Description("The id of the definition.")] Guid id, CancellationToken ct)
    {
        var definition = await FindAsync(id);

        return definition is null ? NotFound() : Ok(MapToResponse(definition));
    }

    /// <summary>
    /// Creates a definition for the current organisation. Refused for an unknown or
    /// non-extendable entity, a malformed path, an unknown kind, a value list the kind does
    /// not allow, a path that a defined scalar shadows or that would shadow one, and an input
    /// over a size limit of <see cref="AddonDefinitionRules"/>. A retired
    /// definition of the same path and kind, also one stored under a retired entity id, is
    /// revived instead of duplicated. The definition is stored under the entity's current id.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <param name="ct">The request's cancellation token.</param>
    [HttpPost]
    [EndpointSummary("Defines an addon property of an entity.")]
    [EndpointDescription(
        "Stores a definition for the caller's organisation and answers it with its id. Its entity, path and kind do not change afterwards; "
        + "a retired definition of the same path and kind is revived, with its id, instead of a second one being stored. " + What)]
    [DescribedRoute(RequestExample = DescribedRouteExamples.CreateAddonDefinition, AnswerExample = DescribedRouteExamples.AddonDefinition)]
    [ProducesResponseType(typeof(AddonDefinitionResponse), StatusCodes.Status201Created, Json, Description = "The definition as it is stored. `Location` names the route that answers it.")]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest, Text, Json, Description = "A rule refuses the definition (the entity, the path, the kind, the value list, a size limit, a path that another definition's path is part of). The body is the reason as a sentence." + Unreadable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, ProblemJson, Description = NoCredentials)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, ProblemJson, Description = "The request belongs to no organisation.")]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict, Text, Json, Description = "The path is already defined on the entity, or was defined with another kind and retired. The body is the reason as a sentence.")]
    public async Task<IActionResult> CreateAsync([FromBody, Description("The definition to store.")] CreateAddonDefinitionRequest request, CancellationToken ct)
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

        if (AddonDefinitionRules.CheckTexts(request.DisplayName, request.Description) is { } textError)
            return BadRequest(textError);

        var existing = (await AddonDefinitionSource.ReadAsync(repository, schema.Model, entity, organisation: null, ct)).ToList();
        var same = existing.FirstOrDefault(definition => string.Equals(definition.Path, request.Path, StringComparison.Ordinal));

        if (same is { Retired: false })
            return Conflict($"'{request.Path}' is already defined on '{entity}'.");

        if (same is not null && !string.Equals(same.Kind, request.Kind, StringComparison.Ordinal))
            return Conflict($"'{request.Path}' was defined as {same.Kind} and retired; a definition's kind is immutable.");

        if (AddonDefinitionRules.CheckShadowing(request.Path, kind, existing) is { } shadowError)
            return BadRequest(shadowError);

        if (AddonDefinitionRules.CheckCount(entity, existing) is { } countError)
            return BadRequest(countError);

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

        // Another request may have stored the path between the read above and this write.
        if (!await TryCommitAsync())
            return Conflict($"'{request.Path}' is already defined on '{entity}'.");

        cache.Invalidate(organizationId, entity);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = definition.Id }, MapToResponse(definition));
    }

    /// <summary>
    /// Updates the labels and the value list of a definition; path and kind never change. A row
    /// stored under a retired entity id moves to the current id (<see cref="MoveToCurrentAsync"/>).
    /// </summary>
    /// <param name="id">The definition id.</param>
    /// <param name="request">The update request.</param>
    /// <param name="ct">The request's cancellation token.</param>
    [HttpPut("{id:guid}")]
    [EndpointSummary("Changes the labels and the value list of an addon definition.")]
    [EndpointDescription(
        "Replaces the display name, the description and the value list of the definition by those of the body; a member left out is cleared. "
        + "Entity, path and kind stay as they are. " + What)]
    [DescribedRoute(RequestExample = DescribedRouteExamples.UpdateAddonDefinition, AnswerExample = DescribedRouteExamples.UpdatedAddonDefinition)]
    [ProducesResponseType(typeof(AddonDefinitionResponse), StatusCodes.Status200OK, Json, Description = "The definition as it is stored.")]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest, Text, Json, Description = "A rule refuses the value list or a text (the kind takes no value list, a size limit). The body is the reason as a sentence." + Unreadable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, ProblemJson, Description = NoCredentials)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, ProblemJson, Description = NoSuchDefinition)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict, Text, Json, Description = DefinedMeanwhile)]
    public async Task<IActionResult> UpdateAsync(
        [Description("The id of the definition.")] Guid id,
        [FromBody, Description("The labels and the value list the definition gets.")] UpdateAddonDefinitionRequest request,
        CancellationToken ct)
    {
        var definition = await FindAsync(id);

        if (definition is null)
            return NotFound();

        var kind = AddonDefinitionRules.ParseKind(definition.Kind) ?? global::OxQL.Model.Addon.AddonKind.Object;
        var values = MapValues(request.Values);

        if (AddonDefinitionRules.CheckValues(kind, values) is { } valuesError)
            return BadRequest(valuesError);

        if (AddonDefinitionRules.CheckTexts(request.DisplayName, request.Description) is { } textError)
            return BadRequest(textError);

        var current = await MoveToCurrentAsync(definition, ct);

        definition.Values = values;
        definition.DisplayName = request.DisplayName;
        definition.Description = request.Description;

        await repository.UpdateAsync(definition);

        if (!await TryCommitAsync())
            return Conflict(MovedOnto(definition, current));

        cache.Invalidate(definition.OrganizationId, current);

        return Ok(MapToResponse(definition));
    }

    /// <summary>
    /// Retires a definition: the row stays, the key is opaque again. A row stored under a retired
    /// entity id moves to the current id (<see cref="MoveToCurrentAsync"/>).
    /// </summary>
    /// <param name="id">The definition id.</param>
    /// <param name="ct">The request's cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [EndpointSummary("Retires an addon definition.")]
    [EndpointDescription(
        "The definition stays stored and is flagged retired; its key is no property of the entity's `addon` member any more, and the values stored under it stay. "
        + "A retired definition is retired again without an effect, and `POST /AddonDefinition` with its path and kind revives it. " + What)]
    [DescribedRoute]
    [ProducesResponseType(StatusCodes.Status204NoContent, Description = "The definition is retired.")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, ProblemJson, Description = NoCredentials)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, ProblemJson, Description = NoSuchDefinition)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict, Text, Json, Description = DefinedMeanwhile)]
    public async Task<IActionResult> DeleteAsync([Description("The id of the definition.")] Guid id, CancellationToken ct)
    {
        var definition = await FindAsync(id);

        if (definition is null)
            return NotFound();

        if (!definition.Retired)
        {
            var current = await MoveToCurrentAsync(definition, ct);

            definition.Retired = true;

            await repository.UpdateAsync(definition);

            if (!await TryCommitAsync())
                return Conflict(MovedOnto(definition, current));

            cache.Invalidate(definition.OrganizationId, current);
        }

        return NoContent();
    }

    /// <summary>
    /// Moves a row stored under a retired entity id to the entity's current id, unless the current
    /// id already holds a row of the same path: then the row stays under its id, so one id never
    /// holds two rows of one path, and the union still reads the live one of them. Answers the
    /// current id, the key the engine's cache holds the entity under.
    /// </summary>
    private async Task<string> MoveToCurrentAsync(AddonDefinitionDocument definition, CancellationToken ct)
    {
        var current = AddonDefinitionSource.CurrentId(schema.Model, definition.Entity);

        if (string.Equals(current, definition.Entity, StringComparison.Ordinal))
            return current;

        var held = await repository.GetByEntitiesAsync([current], definition.OrganizationId, ct);

        if (!held.Any(row => row.Id != definition.Id && string.Equals(row.Path, definition.Path, StringComparison.Ordinal)))
            definition.Entity = current;

        return current;
    }

    /// <summary>
    /// Commits the write, and answers false when the store refused it because the organisation
    /// already holds a row of that entity id and path. The checks before a write read first and
    /// write then, so two requests can pass them together; the store's unique index decides
    /// between them, and the one it refuses is answered as the conflict the check would have
    /// found. Nothing of the refused write is stored.
    /// </summary>
    private async Task<bool> TryCommitAsync()
    {
        try
        {
            await repository.CommitAsync();

            return true;
        }
        catch (AddonDefinitionConflictException)
        {
            return false;
        }
    }

    /// <summary>
    /// The conflict of a row that was to move to its entity's current id (<see cref="MoveToCurrentAsync"/>)
    /// while another request stored its path there. Sent again, the request finds that row and
    /// leaves this one under its id.
    /// </summary>
    private static string MovedOnto(AddonDefinitionDocument definition, string current) =>
        $"'{definition.Path}' was defined on '{current}' while this request was written; send it again.";

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

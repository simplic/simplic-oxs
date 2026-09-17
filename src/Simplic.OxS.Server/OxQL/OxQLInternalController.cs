using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OxQL.AspNetCore;
using OxQL.AspNetCore.Batch;
using OxQL.AspNetCore.Models;
using OxQL.Core.Engine;
using OxQL.Core.Models;
using Simplic.OxS.Server.Controller;

namespace Simplic.OxS.Server.OxQL;

/// <summary>
/// The internal batch route another service's query engine resolves remote references
/// through: <c>POST internal/oxql/batch</c>, admitted by the internal api key, scoped by the
/// forwarded user and organisation headers like every internal call, and executed by the same
/// query service as the public batch. Hidden from the API explorer: it is a cluster route.
/// </summary>
[ApiController]
[Route("internal/oxql")]
[ApiExplorerSettings(IgnoreApi = true)]
[TypeFilter(typeof(RequestSizeFilter))]
public sealed class OxQLInternalController(IOxQLQueryService queryService, ILogger<OxQLInternalController> logger) : OxSInternalController
{
    /// <summary>Executes several queries in order under one time ceiling; always 200 with one outcome per entry.</summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(BatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> BatchAsync([FromBody] BatchRequest batch, CancellationToken ct)
    {
        var outcome = await queryService.BatchAsync(batch, ct);

        switch (outcome)
        {
            case BatchOutcome.Success success:
                return Ok(success.Response);

            case BatchOutcome.Refused refused:
                logger.LogInformation("OxQL internal batch refused {Type} {Code}: {Message}", refused.Refusal.Type, refused.Refusal.Errors?[0].Code, refused.Refusal.Errors?[0].Message);
                return refused.Refusal.ToActionResult();

            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

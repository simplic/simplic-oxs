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
/// The internal routes another service's query engine reaches this host's entities through:
/// <c>POST internal/oxql/batch</c> (remote resolves, semi-joins, keyed fetches and continued
/// stages) and <c>POST internal/oxql/explain</c> (the check of the parts of a query continued
/// here). Admitted by the internal api key, scoped by the forwarded user and organisation
/// headers like every internal call, and served by the same query service as the public routes,
/// as an internal call: the route is the signal, so only here does a request carry the keyed
/// fetch's <c>keyedBy</c>. Hidden from the API explorer: they are cluster routes.
/// </summary>
[ApiController]
[Route("internal/oxql")]
[ApiExplorerSettings(IgnoreApi = true)]
[TypeFilter(typeof(RequestSizeFilter))]
public sealed class OxQLInternalController(IOxQLQueryService queryService, OxQLOptions options, ILogger<OxQLInternalController> logger) : OxSInternalController
{
    /// <summary>Executes several queries in order under one time ceiling; always 200 with one outcome per entry.</summary>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(BatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> BatchAsync([FromBody] BatchRequest batch, CancellationToken ct)
    {
        var outcome = await queryService.BatchAsync(batch, internalCall: true, ct);

        switch (outcome)
        {
            case BatchOutcome.Success success:
                return Ok(success.Response);

            case BatchOutcome.Refused refused:
                return Log("batch", refused.Refusal).ToActionResult();

            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Explains a request without executing it, the same body and answer as the public
    /// <c>POST /oxql/explain</c>: a request that does not bind is 200 with <c>valid: false</c>.
    /// 404 while this host's explain is switched off (<c>OxQL:Explain:Enabled</c>), as the public
    /// route is; the origin then notes the parts unchecked.
    /// </summary>
    [HttpPost("explain")]
    [ProducesResponseType(typeof(ExplainResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> ExplainAsync([FromBody] ExplainRequest request, CancellationToken ct)
    {
        if (!options.Explain.Enabled)
            return NotFound();

        var outcome = await queryService.ExplainAsync(request, internalCall: true, ct);

        return outcome switch
        {
            ExplainOutcome.Success success => Ok(success.Result),
            ExplainOutcome.Refused refused => Log("explain", refused.Refusal).ToActionResult(),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private Refusal Log(string route, Refusal refusal)
    {
        var first = refusal.Errors?.FirstOrDefault();

        logger.LogInformation("OxQL internal {Route} refused {Type} {Code}: {Message}", route, refusal.Type, first?.Code, first?.Message);

        return refusal;
    }
}

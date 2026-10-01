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
/// stages) and <c>POST internal/oxql/explain</c> (the checks of the parts of a query continued
/// here, every check of one round of the origin's explain in one call). Admitted by the internal api key, scoped by the forwarded user and organisation
/// headers like every internal call, and served by the same query service as the public routes,
/// as an internal call: the route is the signal, so only here does a request carry the keyed
/// fetch's <c>keyedBy</c>. Hidden from the API explorer: they are cluster routes.
/// <para>
/// The explain route admits at most <c>OxQL:Explain:MaxConcurrentPerCaller</c> explains in flight
/// per calling service (<see cref="RemoteQueryClient.CallerHeader"/>; a call that does not name its
/// service shares one place set): one more is 429 with <c>Retry-After</c> and <c>EXPLAIN_LIMIT</c>,
/// before anything is bound, and the origin notes the parts unchecked. A call is one place,
/// however many checks it carries (at most <c>OxQL:Explain:MaxBatchChecks</c>). The call's deadline
/// and the owner calls it has left ride in the body (<c>budget</c>), for all its checks together, so
/// a chain of owners never does more than the origin's explain may.
/// </para>
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
    /// Explains the checks of one round of an origin's explain without executing anything: the body is
    /// <c>{ checks: [ explain envelope, … ], budget?: { ms, calls } }</c>, the answer
    /// <c>{ answers: [ … ] }</c> with one entry per check, in order. An entry is the check's explain
    /// answer in its slim form (what an origin reads of an owner: <c>valid</c>, <c>errors</c>, the notes
    /// about the answer itself, <c>stages</c> with their reads and creates, <c>aliases</c>,
    /// <c>types</c>, <c>catalog</c>, <c>owners</c>, <c>revision</c>, <c>cache</c>, <c>engine</c>); a
    /// check that does not bind is an entry with <c>valid: false</c>, as it is on the public route. An
    /// entry is null where a check was refused before binding or the budget ran out before it.
    /// The checks are explained together: what they ask this host's own owners in a round is one call
    /// per owner. 404 while this host's explain is switched off (<c>OxQL:Explain:Enabled</c>), as the
    /// public route is; the origin then notes the parts unchecked. A batch without checks, or with more
    /// than <c>OxQL:Explain:MaxBatchChecks</c>, is 400.
    /// </summary>
    [HttpPost("explain")]
    [ProducesResponseType(typeof(ExplainBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(Refusal), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ExplainAsync([FromBody] ExplainBatchRequest batch, CancellationToken ct)
    {
        if (!options.Explain.Enabled)
            return NotFound();

        var caller = Request.Headers.TryGetValue(RemoteQueryClient.CallerHeader, out var named) && named.FirstOrDefault() is { Length: > 0 } name ? name : UnnamedCaller;

        using var lease = ExplainRateLimiter.For(options).AcquireCaller(caller);

        if (!lease.Acquired)
        {
            Response.Headers.RetryAfter = lease.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

            return Log("explain", lease.Refusal()).ToActionResult();
        }

        var outcome = await queryService.ExplainBatchAsync(batch, ct);

        return outcome switch
        {
            ExplainBatchOutcome.Success success => Ok(success.Response),
            ExplainBatchOutcome.Refused refused => Log("explain", refused.Refusal).ToActionResult(),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>The caller of an internal explain that does not name its service: all of them share one set of places.</summary>
    private const string UnnamedCaller = "(unnamed)";

    private Refusal Log(string route, Refusal refusal)
    {
        var first = refusal.Errors?.FirstOrDefault();

        logger.LogInformation("OxQL internal {Route} refused {Type} {Code}: {Message}", route, refusal.Type, first?.Code, first?.Message);

        return refusal;
    }
}

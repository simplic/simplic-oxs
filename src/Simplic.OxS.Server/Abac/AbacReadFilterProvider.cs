using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Simplic.OxS.ABAC.Policy;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.Data.MongoDB.Abac;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// Server implementation of <see cref="IAbacReadFilterProvider"/> (§7.6 egress): resolves the organization
/// from <see cref="IRequestContext"/> and delegates to <see cref="IAbacEvaluator.BuildListFilterAsync"/>.
/// Without an organization in the request context the read is denied.
/// </summary>
public class AbacReadFilterProvider : IAbacReadFilterProvider
{
    private readonly IAbacEvaluator _evaluator;
    private readonly IRequestContext _requestContext;

    /// <summary>Initializes a new instance of <see cref="AbacReadFilterProvider"/>.</summary>
    public AbacReadFilterProvider(IAbacEvaluator evaluator, IRequestContext requestContext)
    {
        _evaluator = evaluator;
        _requestContext = requestContext;
    }

    /// <inheritdoc/>
    public Task<AbacDecisionResult> GetReadFilterAsync(string action, CancellationToken ct = default)
    {
        var organizationId = _requestContext.OrganizationId;
        if (organizationId == null || organizationId == Guid.Empty)
            return Task.FromResult(new AbacDecisionResult { Decision = AbacDecision.Deny });

        return _evaluator.BuildListFilterAsync(organizationId.Value, action, ct);
    }
}

/// <summary>
/// MVC exception filter mapping <see cref="AbacAccessDeniedException"/> (thrown by ABAC-enabled repositories on
/// list reads) to <c>403 Forbidden</c>. Registered globally by <c>AddAbacRuntime</c>.
/// </summary>
public class AbacAccessDeniedExceptionFilter : IExceptionFilter
{
    private readonly ILogger<AbacAccessDeniedExceptionFilter> _logger;

    /// <summary>Initializes a new instance of <see cref="AbacAccessDeniedExceptionFilter"/>.</summary>
    public AbacAccessDeniedExceptionFilter(ILogger<AbacAccessDeniedExceptionFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AbacAccessDeniedException denied)
            return;

        _logger.LogInformation("ABAC: Access denied for action '{Action}'.", denied.Action);
        context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        context.ExceptionHandled = true;
    }
}

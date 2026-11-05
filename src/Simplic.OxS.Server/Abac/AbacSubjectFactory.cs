using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Subject;
using Simplic.OxS.ABAC.Sync;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// Builds the ABAC subject for the current HTTP request.
/// </summary>
/// <remarks>
/// Sources:
/// <list type="bullet">
///   <item><c>Id</c> (user GUID) and <c>OId</c> (organisation GUID) — JWT claims (authentication unchanged).</item>
///   <item>Roles and teams — resolved server-side from Identity via <see cref="IAbacSubjectAttributeProvider"/>,
///   keyed by (user, organisation) and cached for <c>Abac:Subject:Ttl</c>. They are <b>not</b> read from the token.</item>
///   <item>E-mail — <c>Email</c> / <see cref="ClaimTypes.Email"/> claim when present, otherwise from the Identity lookup.</item>
/// </list>
/// Fail closed: a missing/invalid <c>Id</c>/<c>OId</c>, a user that is not a member of the organisation, or an
/// unavailable lookup yields <c>null</c> (no subject → DENY).
/// </remarks>
public class AbacSubjectFactory : IAbacSubjectFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAbacSubjectAttributeProvider _attributeProvider;
    private readonly ILogger<AbacSubjectFactory> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AbacSubjectFactory"/>.
    /// </summary>
    public AbacSubjectFactory(IHttpContextAccessor httpContextAccessor, IAbacSubjectAttributeProvider attributeProvider, ILogger<AbacSubjectFactory> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _attributeProvider = attributeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AbacSubject?> BuildSubjectAsync(CancellationToken ct = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || user.Identity?.IsAuthenticated != true)
            return null;

        if (!Guid.TryParse(user.Claims.FirstOrDefault(c => c.Type == "Id")?.Value, out var userId))
            return null;

        if (!Guid.TryParse(user.Claims.FirstOrDefault(c => c.Type == "OId")?.Value, out var orgId))
            return null;

        AbacSubjectAttributes? attributes;
        try
        {
            attributes = await _attributeProvider.GetAttributesAsync(userId, orgId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ABAC: Subject attributes of user '{UserId}' in org '{OrgId}' could not be resolved — no subject (deny).", userId, orgId);
            return null;
        }

        if (attributes == null)
        {
            _logger.LogWarning("ABAC: User '{UserId}' is unknown or not a member of org '{OrgId}' — no subject (deny).", userId, orgId);
            return null;
        }

        var email = user.Claims.FirstOrDefault(c => c.Type == "Email" || c.Type == ClaimTypes.Email)?.Value;

        return new AbacSubject
        {
            OrganizationId = orgId,
            User = new AbacSubjectUser { Id = userId, Email = string.IsNullOrWhiteSpace(email) ? attributes.Email ?? string.Empty : email },
            Roles = [.. attributes.Roles ?? []],
            Teams = [.. (attributes.Teams ?? []).Select(t => new AbacTeam { Id = t.Id, Name = t.Name })]
        };
    }
}

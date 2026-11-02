using Microsoft.AspNetCore.Http;
using Simplic.OxS.ABAC.Runtime;
using Simplic.OxS.ABAC.Subject;
using System.Text.Json;

namespace Simplic.OxS.Server.Abac;

/// <summary>
/// Builds the ABAC subject from the current HTTP context's JWT claims.
/// The identity service embeds roles and teams into the token at login time.
/// </summary>
/// <remarks>
/// Expected JWT claim names:
/// <list type="bullet">
///   <item><c>Id</c> — user GUID</item>
///   <item><c>OId</c> — organisation GUID</item>
///   <item><c>Email</c> — user e-mail address</item>
///   <item><c>Roles</c> — JSON array of role name strings, e.g. <c>["admin","viewer"]</c></item>
///   <item><c>Teams</c> — JSON array of team objects, e.g. <c>[{"Id":"...","Name":"hr"}]</c></item>
/// </list>
/// </remarks>
public class AbacSubjectFactory : IAbacSubjectFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="AbacSubjectFactory"/>.
    /// </summary>
    public AbacSubjectFactory(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public AbacSubject? BuildSubject()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || user.Identity?.IsAuthenticated != true)
            return null;

        if (!Guid.TryParse(user.Claims.FirstOrDefault(c => c.Type == "Id")?.Value, out var userId))
            return null;

        if (!Guid.TryParse(user.Claims.FirstOrDefault(c => c.Type == "OId")?.Value, out var orgId))
            return null;

        var email = user.Claims.FirstOrDefault(c => c.Type == "Email")?.Value ?? string.Empty;
        var roles = ParseJsonStringList(user.Claims.FirstOrDefault(c => c.Type == "Roles")?.Value);
        var teams = ParseTeams(user.Claims.FirstOrDefault(c => c.Type == "Teams")?.Value);

        return new AbacSubject
        {
            OrganizationId = orgId,
            User = new AbacSubjectUser { Id = userId, Email = email },
            Roles = roles,
            Teams = teams
        };
    }

    private static IList<string> ParseJsonStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            // Fallback: treat the raw claim value as a single role.
            return [json];
        }
    }

    private static IList<AbacTeam> ParseTeams(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<AbacTeam>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

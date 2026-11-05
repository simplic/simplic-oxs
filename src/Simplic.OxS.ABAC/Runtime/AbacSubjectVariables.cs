using System.Text.RegularExpressions;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Cross-attribute variables (§13.1 P3): a resource condition value may reference the
/// current subject via <c>"{subject.&lt;field&gt;}"</c>, e.g.
/// <c>{"ownerId": {"$eq": "{subject.user.id}"}}</c>. The compiler keeps the placeholder;
/// the runtime substitutes it per request before filters are applied.
/// </summary>
public static partial class AbacSubjectVariables
{
    /// <summary>Supported subject fields.</summary>
    public static readonly IReadOnlyList<string> SupportedFields =
        ["user.id", "user.email", "roles", "teams.id", "teams.name"];

    /// <summary>Matches a whole-string placeholder, group 1 = subject field.</summary>
    [GeneratedRegex(@"^\{subject\.([a-zA-Z0-9_.]+)\}$")]
    public static partial Regex PlaceholderRegex();

    /// <summary>Returns the subject field if <paramref name="value"/> is a placeholder.</summary>
    public static bool TryGetField(object? value, out string field)
    {
        field = string.Empty;
        if (value is not string s)
            return false;
        var m = PlaceholderRegex().Match(s);
        if (!m.Success)
            return false;
        field = m.Groups[1].Value;
        return true;
    }
}
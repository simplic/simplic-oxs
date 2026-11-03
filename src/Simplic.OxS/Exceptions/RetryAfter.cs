using System.Globalization;

namespace Simplic.OxS.Exceptions;

/// <summary>
/// Helpers for formatting the HTTP <c>Retry-After</c> header value.
/// </summary>
internal static class RetryAfter
{
    /// <summary>
    /// Formats <paramref name="value"/> as a non-negative <c>delta-seconds</c> value per RFC 9110.
    /// </summary>
    /// <param name="value">The wait hint. Negative values are clamped to zero.</param>
    /// <returns>The whole-second delay as an invariant-culture string.</returns>
    public static string ToDeltaSeconds(TimeSpan value)
    {
        var seconds = (long)Math.Ceiling(value.TotalSeconds);
        if (seconds < 0)
            seconds = 0;

        return seconds.ToString(CultureInfo.InvariantCulture);
    }
}

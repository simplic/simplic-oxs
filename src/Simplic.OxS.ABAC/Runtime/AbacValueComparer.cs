using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace Simplic.OxS.ABAC.Runtime;

/// <summary>
/// Shared value-comparison helpers for the in-process condition evaluators
/// (<see cref="AbacConditionEvaluator"/> and <see cref="AbacResourceConditionEvaluator"/>).
/// </summary>
internal static class AbacValueComparer
{
    /// <summary>Converts <see cref="JsonElement"/> values into plain CLR values; other values are returned unchanged.</summary>
    public static object? Normalize(object? value)
    {
        if (value is not JsonElement je)
            return value;

        return je.ValueKind switch
        {
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number when je.TryGetInt64(out var l) => l,
            JsonValueKind.Number => je.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Array => je.EnumerateArray().Select(e => Normalize(e)).ToList(),
            JsonValueKind.Object => je.EnumerateObject().ToDictionary(p => p.Name, p => Normalize(p.Value)),
            _ => je.ToString()
        };
    }

    /// <summary>Returns <c>true</c> for enumerable values that are not strings or dictionaries.</summary>
    public static bool IsList(object? value)
        => value is IEnumerable and not string and not IDictionary;

    /// <summary>Case-insensitive equality; handles strings, Guids, numbers and general object equality.</summary>
    public static bool EqualsCi(object? a, object? b)
    {
        a = Normalize(a);
        b = Normalize(b);

        if (a is null || b is null)
            return a is null && b is null;

        if (a is string sa && b is string sb)
            return string.Equals(sa, sb, StringComparison.OrdinalIgnoreCase);

        if (a is Guid ga)
            return b is Guid gb2 ? ga == gb2 : Guid.TryParse(b.ToString(), out var gb) && ga == gb;

        if (b is Guid gbb)
            return Guid.TryParse(a.ToString(), out var gaa) && gaa == gbb;

        if (IsNumeric(a) && IsNumeric(b))
            return ToDouble(a) == ToDouble(b);

        return Equals(a, b);
    }

    /// <summary>Returns <c>true</c> when <paramref name="value"/> is contained in the list <paramref name="listValue"/>.</summary>
    public static bool ContainedIn(object? value, object? listValue)
    {
        listValue = Normalize(listValue);
        if (!IsList(listValue))
            return false;

        foreach (var item in (IEnumerable)listValue!)
        {
            if (EqualsCi(value, item))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="haystack"/> contains <paramref name="needle"/>.
    /// For strings: substring match (case-insensitive). For collections: any element equals needle.
    /// </summary>
    public static bool ContainsValue(object? haystack, object? needle)
    {
        haystack = Normalize(haystack);
        needle = Normalize(needle);

        if (haystack is string hs && needle is string ns)
            return hs.Contains(ns, StringComparison.OrdinalIgnoreCase);

        if (IsList(haystack))
        {
            foreach (var item in (IEnumerable)haystack!)
            {
                if (EqualsCi(item, needle))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Compares two values. Returns <c>null</c> when the values are not comparable
    /// (callers treat that as "condition not satisfied").
    /// </summary>
    public static int? Compare(object? a, object? b)
    {
        a = Normalize(a);
        b = Normalize(b);

        if (a is null || b is null)
            return null;

        if (IsNumeric(a) && IsNumeric(b))
            return ToDouble(a).CompareTo(ToDouble(b));

        if (a is DateTime || a is DateTimeOffset || b is DateTime || b is DateTimeOffset)
        {
            if (TryToDateTimeOffset(a, out var da) && TryToDateTimeOffset(b, out var db))
                return da.CompareTo(db);
            return null;
        }

        if (a is string sa && b is string sb)
            return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);

        if (a is IComparable ca)
        {
            try
            {
                return ca.CompareTo(Convert.ChangeType(b, a.GetType(), CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
            {
                return null;
            }
        }

        return null;
    }

    private static bool IsNumeric(object value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static double ToDouble(object value)
        => Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static bool TryToDateTimeOffset(object value, out DateTimeOffset result)
    {
        switch (value)
        {
            case DateTimeOffset dto:
                result = dto;
                return true;
            case DateTime dt:
                result = new DateTimeOffset(dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt);
                return true;
            case string s:
                return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
            default:
                result = default;
                return false;
        }
    }
}

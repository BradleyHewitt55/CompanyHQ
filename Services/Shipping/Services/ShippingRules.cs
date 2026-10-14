using System.Globalization;

using CompanyHQ.Shipping.Contracts.Enums;

namespace CompanyHQ.Shipping.Services;

/// <summary>The two measurements a quote rests on: does a postal code fall in a rule, and how far apart are two points.</summary>
internal static class ShippingRules
{
    private const double EarthRadiusKm = 6371.0;

    public static bool Matches(PostalCodeRuleType ruleType, string pattern, string postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode) || string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        return ruleType switch
        {
            PostalCodeRuleType.Exact => string.Equals(postalCode, pattern.Trim(), StringComparison.OrdinalIgnoreCase),
            PostalCodeRuleType.Range => TryParseRange(pattern, out var from, out var to)
                && int.TryParse(postalCode, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
                && code >= from && code <= to,
            PostalCodeRuleType.Wildcard => MatchesWildcard(pattern.Trim(), postalCode),
            _ => false
        };
    }

    public static bool TryParseRange(string pattern, out int from, out int to)
    {
        from = to = 0;
        var parts = pattern.Split('-');

        return parts.Length == 2
            && int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out from)
            && int.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out to)
            && from <= to;
    }

    /// <summary>Straight-line distance over the earth's surface.</summary>
    public static double DistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var latitude = Radians(latitude2 - latitude1);
        var longitude = Radians(longitude2 - longitude1);
        var a = Math.Sin(latitude / 2) * Math.Sin(latitude / 2)
            + Math.Cos(Radians(latitude1)) * Math.Cos(Radians(latitude2))
            * Math.Sin(longitude / 2) * Math.Sin(longitude / 2);

        a = Math.Clamp(a, 0, 1);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    // Each '*' is any run of characters. Matched by hand: a pattern is staff input, and a regular
    // expression built from it would let one bad rule make every quote slow.
    private static bool MatchesWildcard(string pattern, string value)
    {
        int patternIndex = 0, valueIndex = 0, star = -1, resume = 0;
        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                star = patternIndex++;
                resume = valueIndex;
            }
            else if (patternIndex < pattern.Length
                && char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex]))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (star >= 0)
            {
                patternIndex = star + 1;
                valueIndex = ++resume;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}

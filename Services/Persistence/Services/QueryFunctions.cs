using System.Text;
using System.Text.RegularExpressions;

namespace CompanyHQ.Persistence.Services;

/// <summary>
/// Functions a query expression may call that a store evaluates itself. Each also works in memory
/// with the same result, so a rule written with one behaves identically in a test double.
/// </summary>
public static class QueryFunctions
{
    /// <summary>
    /// Whether <paramref name="value"/> matches <paramref name="pattern"/>, ignoring case. In the
    /// pattern <c>%</c> stands for any run of characters, <c>_</c> for exactly one, and a backslash
    /// makes the next character literal. A null value never matches.
    /// </summary>
    /// <remarks>
    /// The caller builds the pattern, so the caller decides whether a user's own <c>%</c> is a
    /// wildcard. Escape it with a backslash when it must not be.
    /// </remarks>
    public static bool LikeIgnoreCase(string? value, string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (value is null)
        {
            return false;
        }

        var expression = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            expression.Append(character switch
            {
                '\\' when index + 1 < pattern.Length => Regex.Escape(pattern[++index].ToString()),
                '%' => ".*",
                '_' => ".",
                _ => Regex.Escape(character.ToString())
            });
        }

        return Regex.IsMatch(
            value,
            expression.Append('$').ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }
}

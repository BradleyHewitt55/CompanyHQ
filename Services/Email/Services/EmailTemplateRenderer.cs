using System.Net;
using System.Text.RegularExpressions;
using CompanyHQ.Email.Contracts.Dtos;

namespace CompanyHQ.Email.Services;

internal static partial class EmailTemplateRenderer
{
    private const string DangerousMarkupError =
        "Scripts, embedded documents, event handlers, and dangerous URLs are not allowed.";

    [GeneratedRegex(@"\{\{(?<name>[A-Za-z][A-Za-z0-9_]*)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"<\s*/?\s*(script|iframe|object|embed|base)\b|\bon[a-z][a-z0-9_-]*\s*=|javascript\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DangerousMarkupRegex();

    [GeneratedRegex(@"\b(?:href|src|action|formaction|poster|cite)\s*=\s*(?:[""']\s*)?(?:javascript|vbscript|data\s*):", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DangerousUrlRegex();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    public static AdminEmailTemplatePreviewDto Render(
        EmailTemplateDefinition definition,
        string html,
        IReadOnlyDictionary<string, string?> values)
    {
        var errors = Validate(definition, html);
        errors.AddRange(ValidateValues(definition, values));
        if (errors.Count > 0)
        {
            throw new EmailTemplateValidationException(errors);
        }

        var renderedHtml = Replace(html, values, htmlEncode: true, subjectSafe: false);
        var subject = Replace(definition.SubjectTemplate, values, htmlEncode: false, subjectSafe: true);
        var text = Replace(definition.TextTemplate, values, htmlEncode: false, subjectSafe: false);

        if (ContainsDangerousMarkup(renderedHtml))
        {
            throw new EmailTemplateValidationException([DangerousMarkupError]);
        }

        return new AdminEmailTemplatePreviewDto(subject, renderedHtml, text);
    }

    public static List<string> Validate(EmailTemplateDefinition definition, string html)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(html))
        {
            errors.Add("HTML is required.");
            return errors;
        }

        if (html.Length > 200_000)
        {
            errors.Add("HTML must be at most 200,000 characters.");
        }

        if (ContainsDangerousMarkup(html))
        {
            errors.Add(DangerousMarkupError);
        }

        var supported = definition.SupportedPlaceholders.ToHashSet(StringComparer.Ordinal);
        foreach (Match match in PlaceholderRegex().Matches(html))
        {
            var name = match.Groups["name"].Value;
            if (!supported.Contains(name))
            {
                errors.Add($"Placeholder '{name}' is not supported by this template.");
            }
        }

        foreach (var required in definition.RequiredPlaceholders)
        {
            if (!html.Contains($"{{{{{required}}}}}", StringComparison.Ordinal))
            {
                errors.Add($"Required placeholder '{{{{{required}}}}}' is missing.");
            }
        }

        return errors;
    }

    private static bool ContainsDangerousMarkup(string html)
    {
        if (DangerousMarkupRegex().IsMatch(html) || DangerousUrlRegex().IsMatch(html))
        {
            return true;
        }

        foreach (Match tag in TagRegex().Matches(html))
        {
            var normalized = tag.Value;
            for (var pass = 0; pass < 3; pass++)
            {
                if (DangerousMarkupRegex().IsMatch(normalized) || DangerousUrlRegex().IsMatch(normalized))
                {
                    return true;
                }

                var decoded = WebUtility.HtmlDecode(normalized);
                if (string.Equals(decoded, normalized, StringComparison.Ordinal))
                {
                    break;
                }

                normalized = decoded;
            }
        }

        return false;
    }

    private static List<string> ValidateValues(
        EmailTemplateDefinition definition,
        IReadOnlyDictionary<string, string?> values)
    {
        var errors = new List<string>();
        var supported = definition.SupportedPlaceholders.ToHashSet(StringComparer.Ordinal);
        foreach (var key in values.Keys)
        {
            if (!supported.Contains(key))
            {
                errors.Add($"Value '{key}' is not supported by this template.");
            }
        }

        foreach (var required in definition.RequiredPlaceholders)
        {
            if (!values.TryGetValue(required, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Value for required placeholder '{{{{{required}}}}}' is missing.");
            }
        }

        return errors;
    }

    private static string Replace(
        string template,
        IReadOnlyDictionary<string, string?> values,
        bool htmlEncode,
        bool subjectSafe)
    {
        return PlaceholderRegex().Replace(template, match =>
        {
            var value = values.TryGetValue(match.Groups["name"].Value, out var supplied)
                ? supplied ?? string.Empty
                : string.Empty;

            if (subjectSafe)
            {
                return value.Replace('\r', ' ').Replace('\n', ' ');
            }

            return htmlEncode ? WebUtility.HtmlEncode(value) : value;
        });
    }
}

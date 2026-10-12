using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CompanyHQ.Services.Rss.Contracts.Dtos;
using CompanyHQ.Services.Rss.Contracts.Enums;

namespace CompanyHQ.Services.Rss.Services;

/// <summary>
/// Reads what a web page's head says about its feeds and its publisher. It is a tag scanner, not
/// an HTML parser: it never builds a tree, never runs anything, and does one forward pass over a
/// page that has already been cut to a size limit.
/// </summary>
internal static partial class HtmlHead
{
    private const int MaxAttributes = 32;

    public static (PublisherDto Publisher, IReadOnlyList<DiscoveredFeedDto> Feeds) Read(
        byte[] body,
        string? charset,
        Uri pageUri,
        int maxFeeds)
    {
        var html = Decode(body, charset);
        var baseUri = pageUri;
        var seenBase = false;
        var feeds = new List<DiscoveredFeedDto>();
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? title = null, language = null, icon = null, touchIcon = null;
        var links = new List<Dictionary<string, string>>();

        for (var index = 0; index < html.Length;)
        {
            var open = html.IndexOf('<', index);
            if (open < 0 || open + 1 >= html.Length)
            {
                break;
            }

            if (string.CompareOrdinal(html, open, "<!--", 0, 4) == 0)
            {
                var close = html.IndexOf("-->", open + 4, StringComparison.Ordinal);
                index = close < 0 ? html.Length : close + 3;

                continue;
            }

            if (!char.IsAsciiLetter(html[open + 1]))
            {
                // A closing tag, a doctype, or a stray "<". Only the end of the head matters.
                if (StartsWith(html, open, "</head"))
                {
                    break;
                }

                index = open + 1;

                continue;
            }

            var nameEnd = open + 1;
            while (nameEnd < html.Length && (char.IsAsciiLetterOrDigit(html[nameEnd]) || html[nameEnd] == '-'))
            {
                nameEnd++;
            }

            var name = html[(open + 1)..nameEnd].ToLowerInvariant();
            var attributes = ReadAttributes(html, nameEnd, out index);

            switch (name)
            {
                case "body":
                    index = html.Length;
                    break;

                case "html":
                    language ??= Clean(attributes.GetValueOrDefault("lang"));
                    break;

                case "base" when !seenBase:
                    // Only the first base counts, and only an http(s) one can move where links point.
                    seenBase = true;
                    baseUri = Absolute(attributes.GetValueOrDefault("href"), pageUri) is { } declared ? new Uri(declared) : pageUri;
                    break;

                case "link":
                    links.Add(attributes);
                    break;

                case "meta":
                    if ((attributes.GetValueOrDefault("property") ?? attributes.GetValueOrDefault("name")) is { } key
                        && attributes.GetValueOrDefault("content") is { } content)
                    {
                        meta.TryAdd(key.Trim(), content);
                    }

                    break;

                case "title" or "script" or "style" or "noscript" or "template" or "textarea":
                    // Raw text elements: whatever is inside is not markup, so skip to the end tag.
                    var end = html.IndexOf("</" + name, index, StringComparison.OrdinalIgnoreCase);
                    if (name == "title" && title is null)
                    {
                        title = Clean(WebUtility.HtmlDecode(html[index..(end < 0 ? html.Length : end)]));
                    }

                    index = end < 0 ? html.Length : end + name.Length + 2;
                    break;
            }
        }

        // Links are resolved last, because a base element may follow the links it applies to.
        foreach (var link in links)
        {
            var relations = (link.GetValueOrDefault("rel") ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(relation => relation.ToLowerInvariant())
                .ToArray();

            if (Absolute(link.GetValueOrDefault("href"), baseUri) is not { } href)
            {
                continue;
            }

            if (relations.Contains("alternate") && !relations.Contains("stylesheet")
                && FormatOf(link.GetValueOrDefault("type")) is { } format
                && feeds.Count < maxFeeds && feeds.All(feed => feed.Url != href))
            {
                feeds.Add(new DiscoveredFeedDto(href, format, Clean(link.GetValueOrDefault("title"))));
            }
            else if (relations.Contains("icon"))
            {
                icon ??= href;
            }
            else if (relations.Contains("apple-touch-icon"))
            {
                touchIcon ??= href;
            }
        }

        var publisher = new PublisherDto(
            pageUri.GetLeftPart(UriPartial.Authority) + "/",
            Clean(meta.GetValueOrDefault("og:site_name")) ?? Clean(meta.GetValueOrDefault("application-name")) ?? title,
            Clean(meta.GetValueOrDefault("description")) ?? Clean(meta.GetValueOrDefault("og:description")),
            icon ?? touchIcon,
            language);

        return (publisher, feeds);
    }

    /// <summary>Reads a tag's attributes and leaves <paramref name="index"/> just past its closing bracket.</summary>
    private static Dictionary<string, string> ReadAttributes(string html, int start, out int index)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        index = start;

        while (index < html.Length)
        {
            while (index < html.Length && (char.IsWhiteSpace(html[index]) || html[index] == '/'))
            {
                index++;
            }

            if (index >= html.Length || html[index] == '>')
            {
                index = Math.Min(index + 1, html.Length);

                return attributes;
            }

            var nameStart = index;
            while (index < html.Length && !char.IsWhiteSpace(html[index]) && html[index] is not ('=' or '>' or '/'))
            {
                index++;
            }

            var name = html[nameStart..index];
            while (index < html.Length && char.IsWhiteSpace(html[index]))
            {
                index++;
            }

            var value = string.Empty;
            if (index < html.Length && html[index] == '=')
            {
                index++;
                while (index < html.Length && char.IsWhiteSpace(html[index]))
                {
                    index++;
                }

                if (index < html.Length && html[index] is '"' or '\'')
                {
                    var quote = html[index];
                    var close = html.IndexOf(quote, index + 1);
                    close = close < 0 ? html.Length : close;
                    value = html[(index + 1)..close];
                    index = Math.Min(close + 1, html.Length);
                }
                else
                {
                    var valueStart = index;
                    while (index < html.Length && !char.IsWhiteSpace(html[index]) && html[index] != '>')
                    {
                        index++;
                    }

                    value = html[valueStart..index];
                }
            }

            // The first occurrence of an attribute wins, as it does in a browser.
            if (name.Length > 0 && attributes.Count < MaxAttributes)
            {
                attributes.TryAdd(name, WebUtility.HtmlDecode(value));
            }
        }

        return attributes;
    }

    private static FeedFormat? FormatOf(string? type) => type?.Split(';')[0].Trim().ToLowerInvariant() switch
    {
        "application/rss+xml" => FeedFormat.Rss,
        "application/atom+xml" => FeedFormat.Atom,
        _ => null
    };

    private static string? Absolute(string? value, Uri baseUri) =>
        !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(baseUri, value.Trim(), out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Whitespace().Replace(value, " ").Trim() is { Length: > 0 and <= 500 } text ? text : null;

    private static bool StartsWith(string html, int index, string value) =>
        string.Compare(html, index, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>The transport's charset, then a byte order mark, then the page's own meta declaration, then UTF-8.</summary>
    private static string Decode(byte[] body, string? charset)
    {
        var declared = charset;

        if (string.IsNullOrWhiteSpace(declared) && !(body.Length >= 2 && (body[0], body[1]) is (0xEF, 0xBB) or (0xFE, 0xFF) or (0xFF, 0xFE)))
        {
            var match = MetaCharset().Match(Encoding.Latin1.GetString(body, 0, Math.Min(body.Length, 4096)));
            declared = match.Success ? match.Groups[1].Value : null;
        }

        Encoding encoding;
        try
        {
            encoding = string.IsNullOrWhiteSpace(declared) ? Encoding.UTF8 : Encoding.GetEncoding(declared);
        }
        catch (ArgumentException)
        {
            encoding = Encoding.UTF8;
        }

        using var reader = new StreamReader(new MemoryStream(body), encoding, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    [GeneratedRegex("""charset\s*=\s*["']?\s*([A-Za-z0-9_.:-]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaCharset();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}

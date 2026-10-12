using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CompanyHQ.Services.Rss.Contracts.Dtos;
using CompanyHQ.Services.Rss.Contracts.Enums;

namespace CompanyHQ.Services.Rss.Services;

/// <summary>
/// Reads an RSS or Atom document into <see cref="FeedDto"/>. The bytes are untrusted: the XML
/// reader never loads a DTD, never resolves anything external, and stops at a character ceiling.
/// </summary>
internal static partial class FeedParser
{
    private const string Rss1 = "http://purl.org/rss/1.0/";
    private const string Rss09 = "http://my.netscape.com/rdf/simple/0.9/";
    private const string Content = "http://purl.org/rss/1.0/modules/content/";
    private const string DublinCore = "http://purl.org/dc/elements/1.1/";
    private const string Media = "http://search.yahoo.com/mrss/";
    private const int MaxListPerItem = 20;

    public static (FeedDto? Feed, FeedErrorDto? Error) Parse(byte[] body, string? charset, Uri documentUri, int maxItems)
    {
        XElement root;

        try
        {
            var settings = new XmlReaderSettings
            {
                // No DTD means no entity definitions: nothing to expand, and nothing to fetch.
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 1024,
                MaxCharactersInDocument = Math.Max(body.Length, 1024),
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                // Publishers leak control characters into text; that is their content's problem,
                // not a reason to lose the whole feed.
                CheckCharacters = false
            };

            using var reader = OpenReader(body, charset, settings);
            root = XDocument.Load(reader).Root!;
        }
        catch (Exception exception) when (exception is XmlException or DecoderFallbackException or ArgumentException or InvalidOperationException)
        {
            return (null, SafeHttpFetcher.Error(DeclaresDoctype(body) ? FeedErrorCode.ProhibitedXml : FeedErrorCode.MalformedXml));
        }

        return root.Name.LocalName switch
        {
            "rss" when root.Elements().FirstOrDefault(element => element.Name.LocalName == "channel") is { } channel =>
                (ReadRss(channel, channel.Elements(), documentUri, maxItems), null),
            "RDF" when root.Elements().FirstOrDefault(element => element.Name.LocalName == "channel") is { } channel =>
                (ReadRss(channel, root.Elements(), documentUri, maxItems), null),
            "feed" => (ReadAtom(root, documentUri, maxItems), null),
            _ => (null, SafeHttpFetcher.Error(FeedErrorCode.NotAFeed))
        };
    }

    /// <summary>
    /// A byte order mark or the document's own declaration decides the encoding. Only when the
    /// document says nothing does the transport's charset apply; the reader's default is UTF-8.
    /// </summary>
    private static XmlReader OpenReader(byte[] body, string? charset, XmlReaderSettings settings)
    {
        var hasMark = body.Length >= 2 && (body[0], body[1]) is (0xEF, 0xBB) or (0xFE, 0xFF) or (0xFF, 0xFE);
        var prolog = Encoding.Latin1.GetString(body, 0, Math.Min(body.Length, 256));

        if (!hasMark && !DeclaredEncoding().IsMatch(prolog) && !string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return XmlReader.Create(new StreamReader(new MemoryStream(body), Encoding.GetEncoding(charset)), settings);
            }
            catch (ArgumentException)
            {
                // An encoding name nobody knows: fall through to the reader's own detection.
            }
        }

        return XmlReader.Create(new MemoryStream(body), settings);
    }

    /// <summary>Whether the prolog holds a DOCTYPE, in an 8-bit or a 16-bit encoding. Only names the refusal; the reader enforces it.</summary>
    private static bool DeclaresDoctype(byte[] body)
    {
        var text = new StringBuilder(Math.Min(body.Length, 8192));
        foreach (var value in body.AsSpan(0, Math.Min(body.Length, 8192)))
        {
            if (value != 0) text.Append((char)value);
        }

        return text.ToString().Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
    }

    private static FeedDto ReadRss(XElement channel, IEnumerable<XElement> itemScope, Uri documentUri, int maxItems)
    {
        // Relative links in a feed are relative to the site it describes, when it names one.
        var site = RssUrl(Core(channel, "link"), documentUri, documentUri);
        var baseUri = site is null ? documentUri : new Uri(site);
        var items = itemScope.Where(element => element.Name.LocalName == "item" && IsCore(element)).ToArray();

        return new FeedDto(
            FeedFormat.Rss,
            Plain(Core(channel, "title")?.Value),
            Plain(Core(channel, "description")?.Value),
            site,
            Text(Core(channel, "language") ?? channel.Element(XName.Get("language", DublinCore))),
            RssUrl(Core(channel, "image") is { } image ? Core(image, "url") : null, baseUri, documentUri)
                ?? RssUrl(channel.Parent?.Elements().FirstOrDefault(element => element.Name.LocalName == "image" && IsCore(element)) is { } rdfImage
                    ? Core(rdfImage, "url") : null, baseUri, documentUri),
            FeedDates.Parse(Text(Core(channel, "lastBuildDate") ?? Core(channel, "pubDate") ?? channel.Element(XName.Get("date", DublinCore)))),
            items.Take(maxItems).Select(item => ReadRssItem(item, baseUri, documentUri)).ToArray(),
            Truncated: items.Length > maxItems);
    }

    private static FeedItemDto ReadRssItem(XElement item, Uri baseUri, Uri documentUri)
    {
        var guid = Core(item, "guid");
        var id = Text(guid);
        var link = RssUrl(Core(item, "link"), baseUri, documentUri);

        // A guid is the link unless the publisher says otherwise, so it stands in for a missing one.
        if (link is null && guid is not null && !string.Equals((string?)guid.Attribute("isPermaLink"), "false", StringComparison.OrdinalIgnoreCase))
        {
            link = RssUrl(guid, baseUri, documentUri);
        }

        var media = new List<FeedMediaDto>();
        foreach (var element in item.Descendants())
        {
            var (kind, address) = (element.Name.NamespaceName, element.Name.LocalName) switch
            {
                ("", "enclosure") => (FeedMediaKind.Enclosure, (string?)element.Attribute("url")),
                (Media, "content") => (FeedMediaKind.Enclosure, (string?)element.Attribute("url")),
                (Media, "thumbnail") => (FeedMediaKind.Thumbnail, (string?)element.Attribute("url")),
                _ => (FeedMediaKind.Enclosure, null)
            };

            if (Url(address, RssBase(element, baseUri, documentUri)) is { } url)
            {
                media.Add(new FeedMediaDto(
                    url,
                    kind,
                    Text((string?)element.Attribute("type")),
                    long.TryParse((string?)element.Attribute("length") ?? (string?)element.Attribute("fileSize"),
                        NumberStyles.None, CultureInfo.InvariantCulture, out var length) ? length : null));
            }
        }

        var description = Text(Core(item, "description"));
        var encoded = Text(item.Element(XName.Get("encoded", Content)));

        return new FeedItemDto(
            id,
            link,
            Plain(Core(item, "title")?.Value),
            description is null ? null : Markup(description),
            encoded is null ? null : new FeedContentDto(encoded, FeedContentType.Html),
            Plain(item.Element(XName.Get("creator", DublinCore))?.Value ?? Core(item, "author")?.Value),
            FeedDates.Parse(Text(Core(item, "pubDate") ?? item.Element(XName.Get("date", DublinCore)))),
            null,
            Distinct(media),
            Distinct(item.Elements()
                .Where(element => element.Name.LocalName == "category" && IsCore(element) || element.Name == XName.Get("subject", DublinCore))
                .Select(element => Plain(element.Value))));
    }

    private static FeedDto ReadAtom(XElement feed, Uri documentUri, int maxItems)
    {
        var entries = Atom(feed, "entry").ToArray();
        var feedAuthor = Plain(Atom(feed, "author").Elements().FirstOrDefault(element => element.Name.LocalName == "name")?.Value);
        var image = Atom(feed, "logo").FirstOrDefault() ?? Atom(feed, "icon").FirstOrDefault();

        return new FeedDto(
            FeedFormat.Atom,
            AtomText(Atom(feed, "title").FirstOrDefault()),
            AtomText(Atom(feed, "subtitle").FirstOrDefault() ?? Atom(feed, "tagline").FirstOrDefault()),
            AtomLink(feed, documentUri, "alternate"),
            Text((string?)feed.Attribute(XNamespace.Xml + "lang")),
            image is null ? null : Url(Text(image), BaseOf(image, documentUri)),
            FeedDates.Parse(Text(Atom(feed, "updated").FirstOrDefault() ?? Atom(feed, "modified").FirstOrDefault())),
            entries.Take(maxItems).Select(entry => ReadAtomEntry(entry, documentUri, feedAuthor)).ToArray(),
            Truncated: entries.Length > maxItems);
    }

    private static FeedItemDto ReadAtomEntry(XElement entry, Uri documentUri, string? feedAuthor)
    {
        return new FeedItemDto(
            Text(Atom(entry, "id").FirstOrDefault()),
            AtomLink(entry, documentUri, "alternate"),
            AtomText(Atom(entry, "title").FirstOrDefault()),
            AtomContent(Atom(entry, "summary").FirstOrDefault()),
            AtomContent(Atom(entry, "content").FirstOrDefault()),
            Plain(Atom(entry, "author").Elements().FirstOrDefault(element => element.Name.LocalName == "name")?.Value) ?? feedAuthor,
            FeedDates.Parse(Text(Atom(entry, "published").FirstOrDefault() ?? Atom(entry, "issued").FirstOrDefault() ?? Atom(entry, "created").FirstOrDefault())),
            FeedDates.Parse(Text(Atom(entry, "updated").FirstOrDefault() ?? Atom(entry, "modified").FirstOrDefault())),
            Distinct(Atom(entry, "link")
                .Where(link => string.Equals((string?)link.Attribute("rel"), "enclosure", StringComparison.OrdinalIgnoreCase))
                .Select(link => Url((string?)link.Attribute("href"), BaseOf(link, documentUri)) is { } url
                    ? new FeedMediaDto(url, FeedMediaKind.Enclosure, Text((string?)link.Attribute("type")),
                        long.TryParse((string?)link.Attribute("length"), NumberStyles.None, CultureInfo.InvariantCulture, out var length) ? length : null)
                    : null)
                .Concat(entry.Descendants(XName.Get("thumbnail", Media))
                    .Select(thumbnail => Url((string?)thumbnail.Attribute("url"), BaseOf(thumbnail, documentUri)) is { } url ? new FeedMediaDto(url, FeedMediaKind.Thumbnail) : null))
                .OfType<FeedMediaDto>()),
            Distinct(Atom(entry, "category").Select(category => Plain((string?)category.Attribute("term") ?? (string?)category.Attribute("label")))));
    }

    /// <summary>Children in the feed's own Atom namespace (1.0 or 0.3), so an extension element of the same name is never mistaken for one.</summary>
    private static IEnumerable<XElement> Atom(XElement parent, string name) =>
        parent.Elements().Where(element => element.Name.LocalName == name && element.Name.Namespace == parent.Name.Namespace);

    /// <summary>The link of a relation; a link with no <c>rel</c> is an alternate.</summary>
    private static string? AtomLink(XElement parent, Uri documentUri, string relation)
    {
        var links = Atom(parent, "link")
            .Where(link => string.Equals((string?)link.Attribute("rel") ?? "alternate", relation, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Several alternates are usually one page in several media types; the HTML one is the page.
        var chosen = links.FirstOrDefault(link => ((string?)link.Attribute("type"))?.Contains("html", StringComparison.OrdinalIgnoreCase) ?? false)
            ?? links.FirstOrDefault();

        return chosen is null ? null : Url((string?)chosen.Attribute("href"), BaseOf(chosen, documentUri));
    }

    private static string? AtomText(XElement? element) => element is null
        ? null
        : IsMarkup(element) ? Plain(AtomValue(element)) : Collapse(element.Value);

    private static FeedContentDto? AtomContent(XElement? element)
    {
        if (element is null || AtomValue(element) is not { Length: > 0 } value)
        {
            return null;
        }

        return new FeedContentDto(value, IsMarkup(element) ? FeedContentType.Html : FeedContentType.Text);
    }

    private static bool IsMarkup(XElement element) =>
        ((string?)element.Attribute("type"))?.Trim().ToLowerInvariant() is "html" or "xhtml" or "text/html" or "application/xhtml+xml";

    /// <summary>An XHTML construct carries real elements inside a wrapper div; its markup is what is inside the wrapper.</summary>
    private static string? AtomValue(XElement element)
    {
        var type = ((string?)element.Attribute("type"))?.Trim().ToLowerInvariant();

        if (type is "xhtml" or "application/xhtml+xml")
        {
            var wrapper = element.Elements().FirstOrDefault(child => child.Name.LocalName == "div") ?? element;

            return Text(string.Concat(wrapper.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting))));
        }

        // Anything that is not text or markup (a media type, or content held elsewhere) is not text to hand on.
        return type is null or "text" or "html" or "text/html" or "text/plain" ? Text(element.Value) : null;
    }

    /// <summary>The base address in force at an element: the document's own, refined by every <c>xml:base</c> above it.</summary>
    private static Uri BaseOf(XElement element, Uri documentUri)
    {
        var current = documentUri;

        foreach (var ancestor in element.AncestorsAndSelf().Reverse())
        {
            if ((string?)ancestor.Attribute(XNamespace.Xml + "base") is { } declared && Url(declared, current) is { } resolved)
            {
                current = new Uri(resolved);
            }
        }

        return current;
    }

    private static Uri RssBase(XElement element, Uri fallback, Uri documentUri) =>
        element.AncestorsAndSelf().Any(ancestor => ancestor.Attribute(XNamespace.Xml + "base") is not null)
            ? BaseOf(element, documentUri) : fallback;

    private static string? RssUrl(XElement? element, Uri fallback, Uri documentUri) =>
        element is null ? null : Url(element.Value, RssBase(element, fallback, documentUri));

    /// <summary>An RSS element proper: no namespace in 0.9x and 2.0, the RSS namespace in 1.0.</summary>
    private static bool IsCore(XElement element) => element.Name.NamespaceName is "" or Rss1 or Rss09;

    private static XElement? Core(XElement parent, string name) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == name && IsCore(element));

    private static string? Text(XElement? element) => Text(element?.Value);

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// An absolute http or https address, or nothing. A feed is free to say <c>javascript:</c> or
    /// <c>file:</c>; a caller must never be handed one as if it were a link.
    /// </summary>
    private static string? Url(string? value, Uri baseUri) =>
        Text(value) is { } text && Uri.TryCreate(baseUri, text, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;

    /// <summary>RSS descriptions are markup by convention; one with no tag and no entity is simply text.</summary>
    private static FeedContentDto Markup(string value) =>
        value.Contains('<') || value.Contains('&')
            ? new FeedContentDto(value, FeedContentType.Html)
            : new FeedContentDto(value, FeedContentType.Text);

    /// <summary>Markup reduced to the text a reader would see: tags removed, entities decoded, whitespace collapsed.</summary>
    internal static string? Plain(string? value) =>
        value is null ? null : Collapse(WebUtility.HtmlDecode(Tags().Replace(value, " ")));

    private static string? Collapse(string value) => Text(Whitespace().Replace(value, " "));

    private static IReadOnlyList<T> Distinct<T>(IEnumerable<T?> values) where T : class =>
        values.OfType<T>().Distinct().Take(MaxListPerItem).ToArray();

    [GeneratedRegex("""<\?xml[^>]*\bencoding\s*=""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeclaredEncoding();

    [GeneratedRegex("<[^>]{0,2000}>", RegexOptions.CultureInvariant)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}

/// <summary>Reads the date formats feeds actually use: RFC 822 with its named zones, and ISO 8601.</summary>
internal static partial class FeedDates
{
    private static readonly Dictionary<string, string> Zones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UT"] = "+00:00", ["UTC"] = "+00:00", ["GMT"] = "+00:00", ["Z"] = "+00:00",
        ["EST"] = "-05:00", ["EDT"] = "-04:00", ["CST"] = "-06:00", ["CDT"] = "-05:00",
        ["MST"] = "-07:00", ["MDT"] = "-06:00", ["PST"] = "-08:00", ["PDT"] = "-07:00"
    };

    /// <summary>The instant a publisher wrote, or null when it cannot be read. A wrong date is worse than none.</summary>
    public static DateTimeOffset? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
        {
            return null;
        }

        // The weekday is redundant and often wrong, and a wrong one makes the whole date unreadable.
        var text = Weekday().Replace(value.Trim(), string.Empty);
        text = NamedZone().Replace(text, match => Zones.TryGetValue(match.Value.Trim(), out var offset) ? " " + offset : match.Value);
        text = NumericZone().Replace(text, "$1:$2");

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed
            : null;
    }

    [GeneratedRegex(@"^[A-Za-z]{3,9},?\s+(?=\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Weekday();

    [GeneratedRegex(@"\s+[A-Za-z]{1,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamedZone();

    // Only after a time of day, so the year of a 03-06-2025 date is never taken for an offset.
    [GeneratedRegex(@"(?<=\d{1,2}:\d{2}(?::\d{2})?(?:\.\d+)?\s*)([+-]\d{2})(\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericZone();
}

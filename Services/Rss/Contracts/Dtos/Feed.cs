using CompanyHQ.Rss.Contracts.Enums;

namespace CompanyHQ.Rss.Contracts.Dtos;

/// <summary>One feed document, in the same shape whether the publisher wrote RSS or Atom.</summary>
/// <remarks>
/// Every URL here is absolute and <c>http</c> or <c>https</c>; anything else the publisher wrote is
/// null. Titles, names and categories are plain text. Only <see cref="FeedContentDto"/> can hold
/// markup, and says so.
/// </remarks>
public sealed record FeedDto(
    FeedFormat Format,
    string? Title,
    string? Description,
    /// <summary>The website the feed belongs to, not the feed's own address.</summary>
    string? SiteUrl,
    string? Language,
    string? ImageUrl,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<FeedItemDto> Items,
    /// <summary>True when the document held more entries than the configured maximum and the rest were left out.</summary>
    bool Truncated = false);

/// <summary>One entry of a feed. Everything but the list properties is optional, because publishers omit all of it.</summary>
public sealed record FeedItemDto(
    /// <summary>The publisher's own identifier for the entry (<c>guid</c> or <c>id</c>), verbatim. Not necessarily a URL.</summary>
    string? Id,
    string? Url,
    string? Title,
    FeedContentDto? Summary,
    FeedContentDto? Content,
    string? Author,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<FeedMediaDto> Media,
    IReadOnlyList<string> Categories);

/// <summary>
/// Text a publisher supplied for an entry. When <see cref="Type"/> is
/// <see cref="FeedContentType.Html"/> the value is the publisher's markup exactly as sent:
/// untrusted, and never to be rendered without sanitizing it first. This service does not sanitize.
/// </summary>
public sealed record FeedContentDto(string Value, FeedContentType Type);

/// <summary>A file attached to an entry: an enclosure, a media element, or its thumbnail.</summary>
public sealed record FeedMediaDto(
    string Url,
    FeedMediaKind Kind,
    /// <summary>The publisher's declared media type, unverified.</summary>
    string? Type = null,
    /// <summary>The publisher's declared size in bytes, unverified.</summary>
    long? Length = null);

using System.Text.Json.Serialization;

namespace CompanyHQ.Rss.Contracts.Enums;

/// <summary>Every way a fetch or a discovery can fail. A caller can branch on these; the list only grows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeedErrorCode>))]
public enum FeedErrorCode
{
    /// <summary>Not an absolute http or https address, or one carrying credentials or a port that is not allowed.</summary>
    InvalidUrl = 0,

    /// <summary>The address is, or resolves to, a loopback, private, link-local or otherwise reserved address.</summary>
    UnsafeAddress = 1,

    /// <summary>A redirect pointed somewhere this service will not follow: another scheme, or from https down to http.</summary>
    UnsafeRedirect = 2,

    /// <summary>More redirects than the configured maximum.</summary>
    TooManyRedirects = 3,

    /// <summary>The name did not resolve or the connection could not be made.</summary>
    Unreachable = 4,

    /// <summary>The publisher did not finish answering within the time allowed.</summary>
    Timeout = 5,

    /// <summary>The publisher answered with an error status, carried in <c>HttpStatus</c>.</summary>
    HttpStatus = 6,

    /// <summary>The response was larger than the configured maximum, compressed or decompressed.</summary>
    ResponseTooLarge = 7,

    /// <summary>The response could not be decoded: a broken redirect, an unknown or corrupt content encoding.</summary>
    InvalidResponse = 8,

    /// <summary>The response is well formed but is not an RSS or Atom document.</summary>
    NotAFeed = 9,

    /// <summary>The document is not well-formed XML.</summary>
    MalformedXml = 10,

    /// <summary>The document declares a DTD or entities, which are never processed.</summary>
    ProhibitedXml = 11
}

using System.Text.Json.Serialization;

namespace CompanyHQ.Rss.Contracts.Enums;

/// <summary>How the value of a feed text must be treated.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeedContentType>))]
public enum FeedContentType
{
    /// <summary>Plain text. Encode it before placing it in a page.</summary>
    Text = 0,

    /// <summary>Publisher markup, untrusted. Sanitize it before rendering.</summary>
    Html = 1
}

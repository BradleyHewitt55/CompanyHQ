using System.Text.Json.Serialization;

namespace CompanyHQ.Services.Rss.Contracts.Enums;

/// <summary>What role a file attached to an entry plays.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeedMediaKind>))]
public enum FeedMediaKind
{
    /// <summary>The attached file itself: a podcast episode, a video, a full-size image.</summary>
    Enclosure = 0,

    /// <summary>A small preview image for the entry.</summary>
    Thumbnail = 1
}

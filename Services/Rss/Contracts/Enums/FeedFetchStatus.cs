using System.Text.Json.Serialization;

namespace CompanyHQ.Rss.Contracts.Enums;

/// <summary>What one feed download established.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeedFetchStatus>))]
public enum FeedFetchStatus
{
    /// <summary>Nothing is known: the download or the document failed. First so an unset value never reads as success.</summary>
    Failed = 0,

    /// <summary>A document was read. It may contain no entries.</summary>
    Changed = 1,

    /// <summary>The publisher confirmed the validators still match.</summary>
    NotModified = 2
}

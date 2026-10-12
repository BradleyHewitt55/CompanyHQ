using System.Text.Json.Serialization;

namespace CompanyHQ.Services.Rss.Contracts.Enums;

/// <summary>What looking at one website established.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeedDiscoveryStatus>))]
public enum FeedDiscoveryStatus
{
    /// <summary>The page could not be read. First so an unset value never reads as an answer.</summary>
    Failed = 0,

    /// <summary>At least one feed is advertised, or the address is itself a feed.</summary>
    Found = 1,

    /// <summary>The page was read and advertises no feed.</summary>
    NoneFound = 2
}

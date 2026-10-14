using CompanyHQ.Rss.Contracts.Enums;

namespace CompanyHQ.Rss.Contracts.Dtos;

/// <summary>The outcome of one feed download. Exactly one of the three statuses applies.</summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="FeedFetchStatus.Changed"/>: <see cref="Feed"/> is set. It may hold no items;
/// an empty feed is a real answer and replaces what the caller held.</item>
/// <item><see cref="FeedFetchStatus.NotModified"/>: the publisher confirmed the caller's copy is
/// current. <see cref="Feed"/> is null and the caller keeps its previous items.</item>
/// <item><see cref="FeedFetchStatus.Failed"/>: <see cref="Error"/> is set and nothing is known
/// about the feed. The caller keeps its previous items and validators.</item>
/// </list>
/// </remarks>
public sealed record FeedFetchResultDto(
    FeedFetchStatus Status,
    /// <summary>The address that finally answered, after redirects. Null when no response was read.</summary>
    string? FinalUrl,
    /// <summary>True when every redirect on the way was permanent, so the caller may store <see cref="FinalUrl"/> instead.</summary>
    bool MovedPermanently,
    /// <summary>What to send with the next fetch. On a failure these are the ones the caller sent.</summary>
    FeedValidatorsDto Validators,
    FeedDto? Feed,
    FeedErrorDto? Error);

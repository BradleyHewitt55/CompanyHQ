using CompanyHQ.Services.Rss.Contracts.Enums;

namespace CompanyHQ.Services.Rss.Contracts.Dtos;

/// <summary>The outcome of looking at one website for feeds.</summary>
/// <remarks>
/// <see cref="FeedDiscoveryStatus.NoneFound"/> means the page was read and advertises no feed,
/// which is a real answer. <see cref="FeedDiscoveryStatus.Failed"/> means the page could not be
/// read and nothing is known about it.
/// </remarks>
public sealed record FeedDiscoveryResultDto(
    FeedDiscoveryStatus Status,
    string? FinalUrl,
    PublisherDto? Publisher,
    IReadOnlyList<DiscoveredFeedDto> Feeds,
    FeedErrorDto? Error);

/// <summary>A feed a website advertises. Nothing about it has been fetched or verified.</summary>
public sealed record DiscoveredFeedDto(
    string Url,
    /// <summary>The format the page declares for the link.</summary>
    FeedFormat Format,
    string? Title);

/// <summary>What a website says about itself, as plain text and absolute URLs.</summary>
public sealed record PublisherDto(
    string SiteUrl,
    string? Name,
    string? Description,
    string? IconUrl,
    string? Language);

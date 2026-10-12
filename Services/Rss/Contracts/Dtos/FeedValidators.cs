namespace CompanyHQ.Services.Rss.Contracts.Dtos;

/// <summary>
/// The publisher's own markers for one version of a feed, kept verbatim. Store both and send them
/// back unchanged; they are opaque and only meaningful to the server that issued them.
/// </summary>
public sealed record FeedValidatorsDto(string? ETag = null, string? LastModified = null)
{
    public static FeedValidatorsDto None { get; } = new();

    public bool IsEmpty => ETag is null && LastModified is null;
}

namespace CompanyHQ.Services.Inbox.Contracts.Dtos;

/// <summary>One source of threads the caller's role may open, and whether it can be read right now.</summary>
public sealed record AdminInboxChannelDto(string Key, string Name, bool Available);

/// <summary>One thread in a channel's list. <c>Id</c> is the channel's own identifier, as text.</summary>
public sealed record AdminInboxItemDto(
    string Channel,
    string Id,
    string Title,
    string Author,
    /// <summary>How to reach the author outside the thread, when the channel knows it.</summary>
    string? Contact,
    string Preview,
    /// <summary>Where the thread was started from, such as the site or app that collected it.</summary>
    string? Source,
    /// <summary>The page or screen it was started from, when the channel recorded one.</summary>
    string? Origin,
    bool Open,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt,
    /// <summary>The thread on the channel's own site, when it has one.</summary>
    string? Url);

public sealed record AdminInboxMessageDto(
    string Id,
    string Body,
    string Author,
    bool FromStaff,
    DateTimeOffset CreatedAt);

/// <summary>A thread with its messages, oldest first, and what this channel lets staff do to it.</summary>
public sealed record AdminInboxThreadDto(
    AdminInboxItemDto Item,
    IReadOnlyList<AdminInboxMessageDto> Messages,
    bool CanReply,
    bool CanResolve,
    bool CanDelete);

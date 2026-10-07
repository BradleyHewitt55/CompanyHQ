using CompanyHQ.Services.Inbox.Contracts.Dtos;
using CompanyHQ.Services.Inbox.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;

namespace CompanyHQ.Services.Inbox.Contracts.Ports;

/// <summary>
/// One source of threads in the staff inbox: visitor conversations, GitHub issues, a social account.
/// Zero or more are registered. A service that wants its threads in the inbox does not reference
/// Inbox; the host registers a bridge that implements this over that service.
/// </summary>
/// <remarks>
/// Inbox has already checked <c>inbox.manage</c> and <see cref="Permission"/> before it calls a
/// channel. The acting user is passed on so a channel can hand it to a service that checks again.
/// </remarks>
public interface IInboxChannel
{
    /// <summary>Stable, lowercase, URL-safe. It is the <c>{channel}</c> route segment.</summary>
    string Key { get; }

    string Name { get; }

    /// <summary>The permission a caller needs for this channel on top of <c>inbox.manage</c>.</summary>
    string Permission { get; }

    /// <summary>False while the channel's backend is not connected, so the console can say so.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Open threads first, then most recent activity. A channel honours the search keyword; the
    /// field is honoured only by a channel that can filter on one.
    /// </summary>
    Task<(InboxError Error, PagedResult<AdminInboxItemDto>? Items)> ListAsync(
        Guid actingUserId,
        PageRequest paging,
        SearchRequest? search = null,
        CancellationToken cancellationToken = default);

    Task<(InboxError Error, AdminInboxThreadDto? Thread)> GetAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default);

    Task<InboxError> ReplyAsync(
        Guid actingUserId,
        string id,
        string body,
        CancellationToken cancellationToken = default);

    Task<InboxError> ResolveAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default);

    Task<InboxError> DeleteAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default);
}

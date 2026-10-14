using CompanyHQ.Inbox.Contracts.Dtos;
using CompanyHQ.Inbox.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;

namespace CompanyHQ.Inbox.Services;

/// <summary>
/// The staff inbox over every plugged-in channel. Each call checks <c>inbox.manage</c> and the
/// channel's own permission on the acting user's role row before the channel is reached.
/// </summary>
public interface IInboxService
{
    /// <summary>The channels this caller's role may open. Empty without <c>inbox.manage</c>.</summary>
    Task<IReadOnlyList<AdminInboxChannelDto>> ChannelsAsync(
        Guid actingUserId,
        CancellationToken cancellationToken = default);

    Task<(InboxError Error, PagedResult<AdminInboxItemDto>? Items)> ListAsync(
        Guid actingUserId,
        string channel,
        PageRequest paging,
        SearchRequest? search = null,
        CancellationToken cancellationToken = default);

    Task<(InboxError Error, AdminInboxThreadDto? Thread)> GetAsync(
        Guid actingUserId,
        string channel,
        string id,
        CancellationToken cancellationToken = default);

    Task<InboxError> ReplyAsync(
        Guid actingUserId,
        string channel,
        string id,
        string body,
        CancellationToken cancellationToken = default);

    Task<InboxError> ResolveAsync(
        Guid actingUserId,
        string channel,
        string id,
        CancellationToken cancellationToken = default);

    Task<InboxError> DeleteAsync(
        Guid actingUserId,
        string channel,
        string id,
        CancellationToken cancellationToken = default);
}

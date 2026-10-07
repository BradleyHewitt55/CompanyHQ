using CompanyHQ.Services.Inbox.Contracts.Dtos;
using CompanyHQ.Services.Inbox.Contracts.Enums;
using CompanyHQ.Services.Inbox.Contracts.Ports;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Inbox.Services;

/// <inheritdoc cref="IInboxService" />
internal sealed class InboxService(
    IEnumerable<IInboxChannel> channels,
    IUserAuthorizationService authorization) : IInboxService
{
    public async Task<IReadOnlyList<AdminInboxChannelDto>> ChannelsAsync(
        Guid actingUserId,
        CancellationToken cancellationToken = default)
    {
        var open = new List<AdminInboxChannelDto>();
        if (!await authorization.HasPermissionAsync(actingUserId, AccessPermissions.ManageInbox, cancellationToken))
        {
            return open;
        }

        foreach (var channel in channels)
        {
            if (await authorization.HasPermissionAsync(actingUserId, channel.Permission, cancellationToken))
            {
                open.Add(new AdminInboxChannelDto(
                    channel.Key, channel.Name, await channel.IsAvailableAsync(cancellationToken)));
            }
        }

        return open;
    }

    public async Task<(InboxError Error, PagedResult<AdminInboxItemDto>? Items)> ListAsync(
        Guid actingUserId,
        string channel,
        PageRequest paging,
        SearchRequest? search = null,
        CancellationToken cancellationToken = default)
    {
        var (error, found) = await OpenAsync(actingUserId, channel, cancellationToken);
        return found is null ? (error, null) : await found.ListAsync(actingUserId, paging, search, cancellationToken);
    }

    public async Task<(InboxError Error, AdminInboxThreadDto? Thread)> GetAsync(
        Guid actingUserId,
        string channel,
        string id,
        CancellationToken cancellationToken = default)
    {
        var (error, found) = await OpenAsync(actingUserId, channel, cancellationToken);
        return found is null ? (error, null) : await found.GetAsync(actingUserId, id, cancellationToken);
    }

    public async Task<InboxError> ReplyAsync(
        Guid actingUserId,
        string channel,
        string id,
        string body,
        CancellationToken cancellationToken = default)
    {
        var (error, found) = await OpenAsync(actingUserId, channel, cancellationToken);
        return found is null ? error : await found.ReplyAsync(actingUserId, id, body, cancellationToken);
    }

    public async Task<InboxError> ResolveAsync(
        Guid actingUserId,
        string channel,
        string id,
        CancellationToken cancellationToken = default)
    {
        var (error, found) = await OpenAsync(actingUserId, channel, cancellationToken);
        return found is null ? error : await found.ResolveAsync(actingUserId, id, cancellationToken);
    }

    public async Task<InboxError> DeleteAsync(
        Guid actingUserId,
        string channel,
        string id,
        CancellationToken cancellationToken = default)
    {
        var (error, found) = await OpenAsync(actingUserId, channel, cancellationToken);
        return found is null ? error : await found.DeleteAsync(actingUserId, id, cancellationToken);
    }

    /// <summary>The channel, once the caller's role is confirmed to hold both permissions it needs.</summary>
    private async Task<(InboxError Error, IInboxChannel? Channel)> OpenAsync(
        Guid actingUserId,
        string key,
        CancellationToken cancellationToken)
    {
        if (!await authorization.HasPermissionAsync(actingUserId, AccessPermissions.ManageInbox, cancellationToken))
        {
            return (InboxError.Forbidden, null);
        }

        var channel = channels.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.Ordinal));
        if (channel is null)
        {
            return (InboxError.NotFound, null);
        }

        return await authorization.HasPermissionAsync(actingUserId, channel.Permission, cancellationToken)
            ? (InboxError.None, channel)
            : (InboxError.Forbidden, null);
    }
}

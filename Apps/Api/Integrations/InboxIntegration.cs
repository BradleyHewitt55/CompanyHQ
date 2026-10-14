
using CompanyHQ.Email.Contracts.Ports;
using CompanyHQ.GitHub.Contracts.Dtos;
using CompanyHQ.GitHub.Contracts.Enums;
using CompanyHQ.GitHub.Services;
using CompanyHQ.Inbox.Contracts.Dtos;
using CompanyHQ.Inbox.Contracts.Enums;
using CompanyHQ.Inbox.Contracts.Ports;
using CompanyHQ.Messages.Contracts;
using CompanyHQ.Messages.Contracts.Dtos;
using CompanyHQ.Messages.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Models;

namespace Api.Integrations;

/// <summary>Mails a staff reply to a visitor conversation through Email's controlled reply template.</summary>
internal sealed class InboxReplyMailer(
    IEmailTemplateSender templates,
    ILogger<InboxReplyMailer> logger) : IInboxReplyMailer
{
    public async Task SendAsync(
        string recipient,
        string body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var result = await templates.SendAsync(
            [recipient],
            EmailTemplateKeys.InboxReply,
            new Dictionary<string, string?> { ["body"] = body },
            idempotencyKey,
            cancellationToken);

        // The reply is already stored and readable in the widget, so a failed queue is only logged.
        if (!result.IsSuccess)
        {
            logger.LogWarning("Inbox reply email {IdempotencyKey} was not queued.", idempotencyKey);
        }
    }
}

/// <summary>Puts the connected repository's issues in the staff inbox: reply comments, resolve closes.</summary>
internal sealed class GitHubInboxChannel(IGitHubService github) : IInboxChannel
{
    public string Key => "github";

    public string Name => "GitHub issues";

    public string Permission => AccessPermissions.ManageGitHub;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        (await github.GetAsync(ResponseAudience.Client, cancellationToken)).IssuesEnabled;

    public async Task<(InboxError Error, PagedResult<AdminInboxItemDto>? Items)> ListAsync(
        Guid actingUserId,
        PageRequest paging,
        SearchRequest? search = null,
        CancellationToken cancellationToken = default)
    {
        var (error, issues) = await github.ListIssuesAsync(actingUserId, paging, search?.Keyword, cancellationToken);
        return issues is null
            ? (Map(error), null)
            : (InboxError.None, new PagedResult<AdminInboxItemDto>(
                issues.Items.Select(Item).ToArray(), issues.Page, issues.PageSize, issues.Total));
    }

    public async Task<(InboxError Error, AdminInboxThreadDto? Thread)> GetAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(id, out var number))
        {
            return (InboxError.NotFound, null);
        }

        var (error, thread) = await github.GetIssueAsync(actingUserId, number, cancellationToken);
        if (thread is null)
        {
            return (Map(error), null);
        }

        // The issue body is the first message; the thread reads as one list.
        var messages = new List<AdminInboxMessageDto>
        {
            new(id, thread.Issue.Body, thread.Issue.Author, FromStaff: false, thread.Issue.CreatedAt)
        };
        messages.AddRange(thread.Comments.Select(comment => new AdminInboxMessageDto(
            comment.Id.ToString(), comment.Body, comment.Author, FromStaff: false, comment.CreatedAt)));

        return (InboxError.None, new AdminInboxThreadDto(
            Item(thread.Issue),
            messages,
            CanReply: true,
            CanResolve: thread.Issue.Open,
            CanDelete: false));
    }

    public async Task<InboxError> ReplyAsync(
        Guid actingUserId,
        string id,
        string body,
        CancellationToken cancellationToken = default) =>
        int.TryParse(id, out var number)
            ? Map(await github.CommentAsync(actingUserId, number, body, cancellationToken))
            : InboxError.NotFound;

    public async Task<InboxError> ResolveAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default) =>
        int.TryParse(id, out var number)
            ? Map(await github.CloseIssueAsync(actingUserId, number, cancellationToken))
            : InboxError.NotFound;

    // GitHub has no API to delete an issue with a repository token.
    public Task<InboxError> DeleteAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(InboxError.Unsupported);

    private AdminInboxItemDto Item(AdminGitHubIssueDto issue) =>
        new(
            Key,
            issue.Number.ToString(),
            issue.Title,
            issue.Author,
            Contact: null,
            issue.Body,
            Source: "GitHub",
            Origin: null,
            issue.Open,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.Url);

    private static InboxError Map(GitHubError error) => error switch
    {
        GitHubError.None => InboxError.None,
        GitHubError.Forbidden => InboxError.Forbidden,
        GitHubError.NotFound => InboxError.NotFound,
        _ => InboxError.Unavailable
    };
}

/// <summary>
/// Puts Message's closed member conversations in the staff inbox for review. They are read-only
/// there: Message lets an operator review and delete a closed conversation, never write into it.
/// </summary>
internal sealed class ConversationInboxChannel(
    IMessageService messages,
    IRepository<User> users) : IInboxChannel
{
    /// <summary>Message pages are ten rows; this bounds how much of a long thread one read loads.</summary>
    private const int MaxPages = 20;

    public string Key => "conversations";

    public string Name => "Closed conversations";

    public string Permission => AccessPermissions.ManageConversations;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<(InboxError Error, PagedResult<AdminInboxItemDto>? Items)> ListAsync(
        Guid actingUserId,
        PageRequest paging,
        SearchRequest? search = null,
        CancellationToken cancellationToken = default)
    {
        var page = await messages.ListClosedConversationsAsync(actingUserId, paging, search?.Keyword, cancellationToken);
        return (InboxError.None, new PagedResult<AdminInboxItemDto>(
            page.Items.Select(conversation =>
            {
                var names = string.Join(" and ", conversation.Participants.Select(participant =>
                    FullName(participant.FirstName, participant.LastName)));
                return Item(
                    conversation.Id,
                    names,
                    conversation.LastMessagePreview ?? string.Empty,
                    conversation.ClosedAt,
                    conversation.LastMessageAt ?? conversation.ClosedAt);
            }).ToArray(),
            page.Page,
            page.PageSize,
            page.Total));
    }

    public async Task<(InboxError Error, AdminInboxThreadDto? Thread)> GetAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var conversationId))
        {
            return (InboxError.NotFound, null);
        }

        var rows = new List<ClientMessageDto>();
        DateTimeOffset closedAt = default;
        string? cursor = null;
        for (var read = 0; read < MaxPages; read++)
        {
            var result = await messages.GetClosedMessagesAsync(
                actingUserId, conversationId, cursor, cancellationToken: cancellationToken);
            if (result.Status is not MessageOperationStatus.Success || result.Value is null)
            {
                return (InboxError.NotFound, null);
            }

            rows.AddRange(result.Value.Messages);
            closedAt = result.Value.ClosedAt ?? closedAt;
            cursor = result.Value.NextCursor;
            if (cursor is null)
            {
                break;
            }
        }

        var senderIds = rows.Select(message => message.SenderUserId).Distinct().ToArray();
        var senders = await users.Query()
            .Where(user => senderIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName })
            .ToDictionaryAsync(user => user.Id, user => FullName(user.FirstName, user.LastName), cancellationToken);
        string Sender(Guid userId) => senders.GetValueOrDefault(userId, "Deleted account");

        var ordered = rows.OrderBy(message => message.CreatedAt).ThenBy(message => message.Id).ToArray();
        return (InboxError.None, new AdminInboxThreadDto(
            Item(
                conversationId,
                string.Join(" and ", senderIds.Select(Sender)),
                ordered.LastOrDefault()?.Body ?? string.Empty,
                closedAt,
                ordered.LastOrDefault()?.CreatedAt ?? closedAt),
            ordered.Select(message => new AdminInboxMessageDto(
                message.Id.ToString(), message.Body, Sender(message.SenderUserId), FromStaff: false, message.CreatedAt)).ToArray(),
            CanReply: false,
            CanResolve: false,
            CanDelete: true));
    }

    public Task<InboxError> ReplyAsync(
        Guid actingUserId,
        string id,
        string body,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(InboxError.Unsupported);

    public Task<InboxError> ResolveAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(InboxError.Unsupported);

    public async Task<InboxError> DeleteAsync(
        Guid actingUserId,
        string id,
        CancellationToken cancellationToken = default) =>
        Guid.TryParse(id, out var conversationId)
            ? (await messages.DeleteAsync(actingUserId, conversationId, cancellationToken)).Status switch
            {
                MessageOperationStatus.Success => InboxError.None,
                MessageOperationStatus.Forbidden => InboxError.Forbidden,
                _ => InboxError.NotFound
            }
            : InboxError.NotFound;

    private AdminInboxItemDto Item(
        Guid id,
        string names,
        string preview,
        DateTimeOffset closedAt,
        DateTimeOffset lastActivityAt) =>
        new(
            Key,
            id.ToString(),
            names.Length == 0 ? "Conversation" : names,
            names,
            Contact: null,
            preview,
            Source: "Members",
            Origin: null,
            Open: false,
            closedAt,
            lastActivityAt,
            Url: null);

    private static string FullName(string? firstName, string? lastName)
    {
        var name = $"{firstName} {lastName}".Trim();
        return name.Length == 0 ? "Unnamed account" : name;
    }
}

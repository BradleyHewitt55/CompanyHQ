using CompanyHQ.GitHub.Contracts.Enums;
using CompanyHQ.GitHub.Contracts.Forms;
using CompanyHQ.GitHub.Services;
using CompanyHQ.Inbox.Contracts.Enums;
using CompanyHQ.Inbox.Services;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Widget.Contracts.Dtos;
using CompanyHQ.Widget.Contracts.Enums;
using CompanyHQ.Widget.Contracts.Ports;

namespace Api.Integrations;

/// <summary>Keeps the widget's visitor conversations in Inbox, where staff answer them.</summary>
internal sealed class InboxWidgetConversations(IVisitorConversationService conversations) : IWidgetConversations
{
    public async Task<ClientWidgetConversationAccessDto> StartAsync(
        WidgetConversationStart start,
        CancellationToken cancellationToken = default)
    {
        var access = await conversations.StartAsync(
            new VisitorConversationStart(start.Name, start.Email, start.Message, start.Source, start.Origin),
            cancellationToken);
        return new ClientWidgetConversationAccessDto(access.Id, access.Token);
    }

    public async Task<ClientWidgetConversationDto?> ReadAsync(
        Guid id,
        string token,
        CancellationToken cancellationToken = default) =>
        await conversations.ReadAsync(id, token, cancellationToken) is { } thread
            ? new ClientWidgetConversationDto(
                thread.Id,
                thread.Name,
                thread.Message,
                thread.CreatedAt,
                thread.ResolvedAt,
                thread.Replies
                    .Select(reply => new ClientWidgetReplyDto(reply.Id, reply.Body, reply.FromVisitor, reply.CreatedAt))
                    .ToArray())
            : null;

    public async Task<WidgetError> SendAsync(
        Guid id,
        string token,
        string body,
        CancellationToken cancellationToken = default) =>
        await conversations.SendAsync(id, token, body, cancellationToken) switch
        {
            InboxError.None => WidgetError.None,
            InboxError.Resolved => WidgetError.Closed,
            InboxError.Invalid => WidgetError.Invalid,
            _ => WidgetError.NotFound
        };
}

/// <summary>Files the widget's bug reports as issues in the connected GitHub repository.</summary>
internal sealed class GitHubWidgetIssueTracker(IGitHubService github) : IWidgetIssueTracker
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        (await github.GetAsync(ResponseAudience.Client, cancellationToken)).IssuesEnabled;

    public async Task<WidgetError> SubmitAsync(WidgetIssue issue, CancellationToken cancellationToken = default) =>
        await github.SubmitIssueAsync(
            new SubmitGitHubIssueRequest(
                issue.Category,
                issue.Title,
                issue.Message,
                issue.Email,
                string.IsNullOrWhiteSpace(issue.Origin) ? issue.Source : $"{issue.Source} ({issue.Origin.Trim()})"),
            cancellationToken) switch
        {
            GitHubError.None => WidgetError.None,
            GitHubError.NotConfigured or GitHubError.NotConnected => WidgetError.Unavailable,
            _ => WidgetError.Failed
        };
}

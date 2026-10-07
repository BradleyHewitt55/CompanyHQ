using CompanyHQ.Services.Widget.Contracts.Dtos;
using CompanyHQ.Services.Widget.Contracts.Enums;
using CompanyHQ.Services.Widget.Contracts.Forms;
using CompanyHQ.Services.Widget.Contracts.Models;
using CompanyHQ.Services.Widget.Contracts.Ports;

namespace CompanyHQ.Services.Widget.Services;

/// <inheritdoc cref="IWidgetService" />
internal sealed class WidgetService(
    WidgetOptions options,
    IWidgetConversations? conversations = null,
    IWidgetIssueTracker? issues = null) : IWidgetService
{
    private static readonly IReadOnlyList<string> IssueCategories = ["Bug", "Feature", "Security", "Other"];

    public async Task<ClientWidgetDto> GetAsync(CancellationToken cancellationToken = default) =>
        new(
            conversations is not null,
            issues is not null && await issues.IsAvailableAsync(cancellationToken),
            options.Sources,
            IssueCategories);

    public async Task<(WidgetError Error, ClientWidgetConversationAccessDto? Access)> StartConversationAsync(
        StartWidgetConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (conversations is null)
        {
            return (WidgetError.Unavailable, null);
        }

        if (Source(request.Source) is not { } source)
        {
            return (WidgetError.UnknownSource, null);
        }

        return (WidgetError.None, await conversations.StartAsync(
            new WidgetConversationStart(request.Name, request.Email, request.Message, source, request.Origin),
            cancellationToken));
    }

    public async Task<(WidgetError Error, ClientWidgetConversationDto? Conversation)> ReadConversationAsync(
        Guid id,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (conversations is null)
        {
            return (WidgetError.Unavailable, null);
        }

        return await conversations.ReadAsync(id, token, cancellationToken) is { } conversation
            ? (WidgetError.None, conversation)
            : (WidgetError.NotFound, null);
    }

    public Task<WidgetError> SendMessageAsync(
        Guid id,
        SendWidgetMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return conversations is null
            ? Task.FromResult(WidgetError.Unavailable)
            : conversations.SendAsync(id, request.Token, request.Body, cancellationToken);
    }

    public async Task<WidgetError> SubmitIssueAsync(
        SubmitWidgetIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (issues is null)
        {
            return WidgetError.Unavailable;
        }

        if (Source(request.Source) is not { } source)
        {
            return WidgetError.UnknownSource;
        }

        return await issues.SubmitAsync(
            new WidgetIssue(request.Category, request.Title, request.Message, request.Email, source, request.Origin),
            cancellationToken);
    }

    /// <summary>The configured spelling of an allowed source, the default when none is named, or null.</summary>
    private string? Source(string? requested) =>
        string.IsNullOrWhiteSpace(requested)
            ? options.Sources[0]
            : options.Sources.FirstOrDefault(source => string.Equals(source, requested.Trim(), StringComparison.OrdinalIgnoreCase));
}

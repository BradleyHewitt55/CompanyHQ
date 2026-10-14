using CompanyHQ.Widget.Contracts.Dtos;
using CompanyHQ.Widget.Contracts.Enums;
using CompanyHQ.Widget.Contracts.Forms;

namespace CompanyHQ.Widget.Services;

/// <summary>
/// The public front door for a contact widget. It stores nothing: it checks where a submission
/// comes from and hands it to whichever destination is plugged in.
/// </summary>
public interface IWidgetService
{
    Task<ClientWidgetDto> GetAsync(CancellationToken cancellationToken = default);

    Task<(WidgetError Error, ClientWidgetConversationAccessDto? Access)> StartConversationAsync(
        StartWidgetConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<(WidgetError Error, ClientWidgetConversationDto? Conversation)> ReadConversationAsync(
        Guid id,
        string token,
        CancellationToken cancellationToken = default);

    Task<WidgetError> SendMessageAsync(
        Guid id,
        SendWidgetMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<WidgetError> SubmitIssueAsync(
        SubmitWidgetIssueRequest request,
        CancellationToken cancellationToken = default);
}

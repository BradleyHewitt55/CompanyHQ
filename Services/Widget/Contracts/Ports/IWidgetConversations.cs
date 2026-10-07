using CompanyHQ.Services.Widget.Contracts.Dtos;
using CompanyHQ.Services.Widget.Contracts.Enums;

namespace CompanyHQ.Services.Widget.Contracts.Ports;

/// <summary>A conversation to open, with the source already checked against the allowed list.</summary>
public sealed record WidgetConversationStart(
    string Name,
    string Email,
    string Message,
    string Source,
    string? Origin);

/// <summary>
/// Where widget conversations are kept and answered. Optional: with no bridge the widget reports
/// messages as unavailable.
/// </summary>
public interface IWidgetConversations
{
    Task<ClientWidgetConversationAccessDto> StartAsync(
        WidgetConversationStart start,
        CancellationToken cancellationToken = default);

    /// <summary>Null for an unknown id and for a wrong token alike.</summary>
    Task<ClientWidgetConversationDto?> ReadAsync(
        Guid id,
        string token,
        CancellationToken cancellationToken = default);

    Task<WidgetError> SendAsync(
        Guid id,
        string token,
        string body,
        CancellationToken cancellationToken = default);
}

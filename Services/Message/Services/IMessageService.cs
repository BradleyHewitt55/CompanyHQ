using CompanyHQ.Services.Messages.Contracts;
using CompanyHQ.Services.Messages.Contracts.Dtos;
using CompanyHQ.Services.Messages.Contracts.Forms;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;

namespace CompanyHQ.Services.Messages.Services;

public interface IMessageService
{
    Task<PagedResult<ClientConversationDto>> ListConversationsAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken cancellationToken = default);

    Task<MessageOperationResult<ClientConversationDto>> StartConversationAsync(
        Guid userId,
        StartConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<MessageOperationResult<MessagePageDto>> GetMessagesAsync(
        Guid userId,
        Guid conversationId,
        string? cursor = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<MessageOperationResult<SendMessageResult>> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<MessageOperationResult<DateTimeOffset>> MarkReadAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<MessageOperationResult<bool>> SetMuteAsync(
        Guid userId,
        Guid conversationId,
        bool muted,
        CancellationToken cancellationToken = default);

    Task<MessageOperationResult<bool>> ClearAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<MessageOperationResult<bool>> CloseAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AdminConversationDto>> ListClosedConversationsAsync(
        Guid administratorId,
        PageRequest paging,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<MessageOperationResult<MessagePageDto>> GetClosedMessagesAsync(
        Guid administratorId,
        Guid conversationId,
        string? cursor = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<MessageOperationResult<ClientConversationDto>> StartOperatorConversationAsync(
        Guid administratorId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

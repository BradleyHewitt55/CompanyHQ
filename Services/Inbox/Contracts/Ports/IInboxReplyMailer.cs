namespace CompanyHQ.Services.Inbox.Contracts.Ports;

/// <summary>
/// Mails a staff reply to the visitor. Optional: with no bridge the reply is still stored and the
/// visitor reads it in the widget.
/// </summary>
public interface IInboxReplyMailer
{
    /// <summary>The same <paramref name="idempotencyKey"/> must send at most once.</summary>
    Task SendAsync(
        string recipient,
        string body,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

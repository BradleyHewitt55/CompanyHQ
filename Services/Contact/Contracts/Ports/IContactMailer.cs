namespace CompanyHQ.Contact.Contracts.Ports;

/// <summary>
/// Queues one contact message for delivery. The host bridges it to whichever email service is
/// plugged in; Contact never references that service.
/// </summary>
public interface IContactMailer
{
    /// <summary>
    /// Returns null when the message was queued, otherwise the reason it was not. The same
    /// <paramref name="idempotencyKey"/> queues once.
    /// </summary>
    Task<string?> SendAsync(
        string recipient,
        IReadOnlyDictionary<string, string?> values,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

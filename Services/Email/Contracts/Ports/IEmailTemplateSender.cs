namespace CompanyHQ.Email.Contracts.Ports;

/// <summary>
/// What another service calls to email someone: a controlled template key and structured values,
/// queued durably in Email's outbox. Callers reference this Contracts assembly only, never the Email
/// service, and DI supplies the implementation when Email is plugged in.
/// </summary>
public interface IEmailTemplateSender
{
    /// <summary>
    /// Renders <paramref name="templateKey"/> with <paramref name="values"/> and queues it. The same
    /// <paramref name="idempotencyKey"/> queues once, so an at-least-once caller may repeat it.
    /// </summary>
    Task<EmailSendResult> SendAsync(
        IReadOnlyList<string> recipients,
        string templateKey,
        IReadOnlyDictionary<string, string?> values,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

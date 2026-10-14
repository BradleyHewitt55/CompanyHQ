using CompanyHQ.Email.Contracts.Enums;

namespace CompanyHQ.Email.Contracts.Ports;

/// <summary>Read the durable outbox outcome without sending or exposing message bodies.</summary>
public interface IEmailDeliveryQuery
{
    Task<EmailDeliveryState?> GetAsync(string idempotencyKey,CancellationToken cancellationToken=default);
}

/// <summary>Sent means provider acceptance; Pending includes queued and retrying work, never inbox delivery.</summary>
public sealed record EmailDeliveryState(Guid QueueId,QueuedEmailStatus Status,string? ProviderReference);

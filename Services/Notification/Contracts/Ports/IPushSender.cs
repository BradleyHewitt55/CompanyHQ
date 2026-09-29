namespace CompanyHQ.Services.Notifications.Contracts.Ports;

/// <summary>
/// Provider-neutral push delivery. An adapter service (Firebase) implements it by referencing this
/// Contracts assembly only. Optional: without one, the push channel is skipped.
/// </summary>
public interface IPushSender
{
    /// <summary>False when the adapter is plugged in but has no credentials; push is then skipped.</summary>
    bool IsConfigured { get; }

    /// <summary>Returns exactly one outcome per token in <paramref name="message"/>.</summary>
    Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default);
}

/// <summary>One push to a set of device tokens. Data-only payloads keep foreground and background alike.</summary>
public sealed record PushMessage(
    IReadOnlyList<string> Tokens,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null);

public sealed record PushSendResult(IReadOnlyList<PushTokenOutcome> Outcomes);

public sealed record PushTokenOutcome(string Token, PushDeliveryStatus Status);

public enum PushDeliveryStatus
{
    Delivered,

    /// <summary>The token is unregistered; its device is disabled and never retried.</summary>
    TokenDead,

    TransientFailure,
    PermanentFailure
}

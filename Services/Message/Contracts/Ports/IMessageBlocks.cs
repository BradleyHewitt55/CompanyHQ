namespace CompanyHQ.Messages.Contracts.Ports;

/// <summary>
/// Who may not message whom. Optional: with no adapter plugged in, nobody is blocked. The host
/// bridges it to whichever service keeps block lists; Message never references that service.
/// </summary>
public interface IMessageBlocks
{
    /// <summary>True when either account has blocked the other.</summary>
    Task<bool> IsBlockedBetweenAsync(Guid a, Guid b, CancellationToken cancellationToken = default);

    /// <summary>Every account <paramref name="userId"/> must not see messages from, in either direction.</summary>
    Task<IReadOnlySet<Guid>> BlockedForAsync(Guid userId, CancellationToken cancellationToken = default);
}

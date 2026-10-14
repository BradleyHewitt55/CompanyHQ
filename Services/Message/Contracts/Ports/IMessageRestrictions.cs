namespace CompanyHQ.Messages.Contracts.Ports;

/// <summary>
/// Message ids an operator has confirmed a report against. Optional: with no adapter plugged in,
/// nothing is restricted.
/// </summary>
public interface IMessageRestrictions
{
    Task<IReadOnlySet<Guid>> RestrictedMessageIdsAsync(CancellationToken cancellationToken = default);
}

namespace CompanyHQ.Messages.Contracts.Ports;

/// <summary>
/// Operator review flags on a sending account. Optional: with no adapter plugged in, no account is
/// flagged and a raised flag goes nowhere.
/// </summary>
public interface IMessageAccountFlags
{
    Task RaiseAsync(Guid userId, string kind, string evidence, CancellationToken cancellationToken = default);

    Task<bool> IsFlaggedAsync(Guid userId, string kind, CancellationToken cancellationToken = default);
}

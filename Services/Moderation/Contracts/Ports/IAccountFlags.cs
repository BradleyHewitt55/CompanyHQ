namespace CompanyHQ.Services.Moderation.Contracts.Ports;

/// <summary>
/// Raising and reading operator review flags on an account. Callers reference this Contracts
/// assembly only and take it as an optional dependency: with Moderation not plugged in, no account is
/// flagged and a raised flag goes nowhere.
/// </summary>
public interface IAccountFlags
{
    Task RaiseAsync(Guid userId, string kind, string evidence, CancellationToken ct = default);

    Task<bool> IsFlaggedAsync(Guid userId, string kind, CancellationToken ct = default);
}

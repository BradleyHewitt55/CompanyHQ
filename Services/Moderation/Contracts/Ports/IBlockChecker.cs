namespace CompanyHQ.Moderation.Contracts.Ports;

/// <summary>
/// Who has blocked whom, for another service to honour. Callers reference this Contracts assembly
/// only and take it as an optional dependency: with Moderation not plugged in, nobody is blocked.
/// </summary>
public interface IBlockChecker
{
    /// <summary>True when either account has blocked the other.</summary>
    Task<bool> IsBlockedBetweenAsync(Guid a, Guid b, CancellationToken ct = default);

    /// <summary>Every account <paramref name="userId"/> must not see content from, in either direction.</summary>
    Task<IReadOnlySet<Guid>> BlockedForAsync(Guid userId, CancellationToken ct = default);
}

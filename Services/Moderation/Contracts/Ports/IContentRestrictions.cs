namespace CompanyHQ.Services.Moderation.Contracts.Ports;

/// <summary>
/// Content ids an operator has confirmed a report against, for the owning service to suppress.
/// Optional for callers: with Moderation not plugged in, nothing is restricted.
/// </summary>
public interface IContentRestrictions
{
    Task<IReadOnlySet<Guid>> RestrictedIdsAsync(string targetKind, CancellationToken ct = default);
}

using CompanyHQ.Services.Moderation.Contracts.Ports;

namespace CompanyHQ.Services.Moderation.Services;

/// <summary>Reads and invalidates the effective global suppression union for confirmed reports.</summary>
public interface IContentRestrictionService : IContentRestrictions
{
    Task InvalidateAsync(string targetKind, CancellationToken ct = default);
}

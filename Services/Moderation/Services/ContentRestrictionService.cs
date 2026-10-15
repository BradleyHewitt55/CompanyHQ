using CompanyHQ.ServiceCatalog.Contracts;

using CompanyHQ.Cache.Services;
using CompanyHQ.Moderation.Contracts.Enums;
using CompanyHQ.Moderation.Contracts.Models;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Moderation.Services;

/// <inheritdoc cref="IContentRestrictionService" />
public sealed class ContentRestrictionService(IServiceAvailability? availability,
    IRepository<ContentReport> reports,
    ICacheService cache) : IContentRestrictionService
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public ContentRestrictionService(
    IRepository<ContentReport> reports,
    ICacheService cache) : this(null, reports, cache) { }

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlySet<Guid>> RestrictedIdsAsync(
        string targetKind,
        CancellationToken ct = default)
    {
        await (availability?.RequireAsync("moderation", false, ct) ?? Task.CompletedTask);
        var normalizedKind = ReportTargetKind.Normalize(targetKind);

        return await cache.GetOrSetAsync<HashSet<Guid>>(
            CacheKey(normalizedKind),
            cancellationToken => reports.Query()
                .Where(report => report.TargetKind == normalizedKind
                    && report.Status == ReportStatus.ActionTaken)
                .Select(report => report.TargetId)
                .Distinct()
                .ToHashSetAsync(cancellationToken),
            CacheTtl,
            ct);
    }

    public Task InvalidateAsync(string targetKind, CancellationToken ct = default) {
        availability?.RequireAsync("moderation", false, ct).GetAwaiter().GetResult();
        return cache.RemoveAsync(CacheKey(ReportTargetKind.Normalize(targetKind)), ct);
    }

    private static string CacheKey(string targetKind) =>
        $"moderation:v1:restricted:{targetKind}";
}

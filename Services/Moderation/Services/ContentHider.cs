
using CompanyHQ.Cache.Services;
using CompanyHQ.Moderation.Contracts.Dtos;
using CompanyHQ.Moderation.Contracts.Enums;
using CompanyHQ.Moderation.Contracts.Forms;
using CompanyHQ.Moderation.Contracts.Models;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Moderation.Services;

/// <inheritdoc cref="IContentHider" />
public sealed class ContentHider(
    IRepository<HiddenContent> hidden,
    ICacheService cache) : IContentHider
{
    private static readonly TimeSpan HiddenCacheTtl = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlySet<Guid>> HiddenIdsAsync(
        Guid userId,
        string targetKind,
        CancellationToken ct = default)
    {
        EnsureUserId(userId, nameof(userId));
        var normalizedKind = ReportTargetKind.Normalize(targetKind);

        return await cache.GetOrSetAsync<HashSet<Guid>>(
            CacheKey(userId, normalizedKind),
            cancellationToken => hidden.Query()
                .Where(item => item.UserId == userId && item.TargetKind == normalizedKind)
                .Select(item => item.TargetId)
                .Distinct()
                .ToHashSetAsync(cancellationToken),
            HiddenCacheTtl,
            ct);
    }

    public async Task HideAsync(
        Guid userId,
        HideContentRequest request,
        HiddenContentSource source = HiddenContentSource.Manual,
        Guid? sourceId = null,
        CancellationToken ct = default)
    {
        EnsureUserId(userId, nameof(userId));
        ArgumentNullException.ThrowIfNull(request);

        if (request.TargetId == Guid.Empty)
        {
            throw new ArgumentException("A target id is required.", nameof(request));
        }

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentException("The hidden-content source is invalid.", nameof(source));
        }

        var normalizedKind = ReportTargetKind.Normalize(request.TargetKind);
        var normalizedSourceId = SourceIdFor(source, sourceId);
        var exists = await hidden.Query()
            .AnyAsync(item => item.UserId == userId
                && item.TargetKind == normalizedKind
                && item.TargetId == request.TargetId
                && item.Source == source
                && item.SourceId == normalizedSourceId, ct);

        if (!exists)
        {
            await hidden.AddAsync(new HiddenContent
            {
                UserId = userId,
                TargetKind = normalizedKind,
                TargetId = request.TargetId,
                Source = source,
                SourceId = normalizedSourceId
            }, ct);
        }

        // The repository write has committed. Never let request cancellation leave a stale hidden
        // cache that could reveal a newly hidden target.
        await InvalidateAsync(userId, normalizedKind, CancellationToken.None);
    }

    public async Task<bool> RemoveSourceAsync(
        Guid userId,
        string targetKind,
        Guid targetId,
        HiddenContentSource source,
        Guid sourceId,
        CancellationToken ct = default)
    {
        EnsureUserId(userId, nameof(userId));
        if (targetId == Guid.Empty)
        {
            return false;
        }

        var normalizedKind = ReportTargetKind.Normalize(targetKind);
        var normalizedSourceId = SourceIdFor(source, sourceId);
        var id = await hidden.Query()
            .Where(item => item.UserId == userId
                && item.TargetKind == normalizedKind
                && item.TargetId == targetId
                && item.Source == source
                && item.SourceId == normalizedSourceId)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(ct);

        if (id is not { } hiddenId || !await hidden.DeleteAsync(hiddenId, ct))
        {
            return false;
        }

        // The repository delete has committed. Keep the effective union synchronized even when the
        // request is cancelled after the write.
        await InvalidateAsync(userId, normalizedKind, CancellationToken.None);
        return true;
    }

    public async Task<bool> UnhideAsync(
        Guid userId,
        string targetKind,
        Guid targetId,
        CancellationToken ct = default)
    {
        EnsureUserId(userId, nameof(userId));
        if (targetId == Guid.Empty)
        {
            return false;
        }

        var normalizedKind = ReportTargetKind.Normalize(targetKind);
        var ids = await hidden.Query()
            .Where(item => item.UserId == userId
                && item.TargetKind == normalizedKind
                && item.TargetId == targetId
                && (item.Source == HiddenContentSource.Manual
                    || item.Source == HiddenContentSource.Legacy))
            .Select(item => item.Id)
            .ToListAsync(ct);

        if (ids.Count == 0)
        {
            // Report and block ownership are deliberate safety decisions. A public unhide cannot
            // silently erase either one, so the caller receives the same 404 as an unknown target.
            return false;
        }

        foreach (var id in ids)
        {
            await hidden.DeleteAsync(id, ct);
        }

        // The repository deletes have committed. Keep the effective union synchronized even when
        // the request is cancelled after the write.
        await InvalidateAsync(userId, normalizedKind, CancellationToken.None);
        return true;
    }

    public async Task<PagedResult<ClientHiddenContentDto>> ListAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken ct = default)
    {
        EnsureUserId(userId, nameof(userId));

        var query = hidden.Query()
            .Where(item => item.UserId == userId)
            .Select(item => new { item.TargetKind, item.TargetId })
            .Distinct()
            .OrderBy(item => item.TargetKind)
            .ThenBy(item => item.TargetId);
        var total = await query.CountAsync(ct);
        var rows = await query
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync(ct);

        return new PagedResult<ClientHiddenContentDto>(
            rows.Select(item => new ClientHiddenContentDto(item.TargetKind, item.TargetId)).ToArray(),
            paging.Page,
            paging.PageSize,
            total);
    }

    private async Task InvalidateAsync(Guid userId, string targetKind, CancellationToken ct) =>
        await cache.RemoveAsync(CacheKey(userId, targetKind), ct);

    private static string CacheKey(Guid userId, string targetKind) =>
        $"moderation:v1:hidden:{userId}:{targetKind}";

    private static Guid SourceIdFor(HiddenContentSource source, Guid? sourceId)
    {
        if (source is HiddenContentSource.Report or HiddenContentSource.Block)
        {
            if (sourceId is not { } id || id == Guid.Empty)
            {
                throw new ArgumentException("A source id is required for this hidden-content source.", nameof(sourceId));
            }

            return id;
        }

        return sourceId.GetValueOrDefault();
    }

    private static void EnsureUserId(Guid userId, string parameterName)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", parameterName);
        }
    }
}

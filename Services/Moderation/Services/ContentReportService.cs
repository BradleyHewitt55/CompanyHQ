using System.Data;
using System.Linq.Expressions;

using Microsoft.Extensions.Logging;

using CompanyHQ.Services.Moderation.Contracts.Ports;
using CompanyHQ.Services.Cache.Services;
using CompanyHQ.Services.Moderation.Contracts.Captures;
using CompanyHQ.Services.Moderation.Contracts.Dtos;
using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Moderation.Contracts.Forms;
using CompanyHQ.Services.Moderation.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Moderation.Services;

/// <inheritdoc cref="IContentReportService" />
public sealed class ContentReportService(
    IPersistence context,
    IRepository<ContentReport> reports,
    IUserAuthorizationService authorization,
    ICacheService cache,
    IContentRestrictionService restrictions,
    IReportTargetRegistry registry,
    IEnumerable<IReportHistoryClearer> historyClearers,
    ILogger<ContentReportService> logger) : IContentReportService
{
    public async Task<ReportResult> CreateAsync(
        Guid reporterUserId,
        CreateReportRequest request,
        CancellationToken ct = default)
    {
        EnsureUserId(reporterUserId, nameof(reporterUserId));
        ArgumentNullException.ThrowIfNull(request);

        if (!registry.TryGet(request.TargetKind, out var target))
        {
            throw new ArgumentException("The target kind is not reportable.", nameof(request));
        }

        if (request.TargetId == Guid.Empty)
        {
            throw new ArgumentException("A target id is required.", nameof(request));
        }

        if (!target.Allows(request.Reason))
        {
            throw new ArgumentException(
                $"The reason '{request.Reason}' is not allowed for target kind '{target.TargetKind}'.",
                nameof(request));
        }

        var details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim();
        if (request.Reason is ReportReason.Other && details is null)
        {
            throw new ArgumentException("Details are required when the reason is Other.", nameof(request));
        }

        if (request.ClearHistory && target.TargetKind is not ("message" or "user"))
        {
            throw new ArgumentException(
                "Chat history can only be cleared when reporting a message or a user.",
                nameof(request));
        }

        if (target.TargetKind == "user" && request.TargetId == reporterUserId)
        {
            return ReportResult.Fail(ReportFailure.SelfTarget);
        }

        if (await reports.Query().AnyAsync(
                report => report.ReporterUserId == reporterUserId
                    && report.TargetKind == target.TargetKind
                    && report.TargetId == request.TargetId
                    && report.Status != ReportStatus.Withdrawn,
                ct))
        {
            return ReportResult.Fail(ReportFailure.Duplicate);
        }

        var captureResult = await CaptureAsync(target, request.TargetId, ct);
        if (!captureResult.Found)
        {
            return ReportResult.Fail(ReportFailure.TargetNotFound);
        }

        var targetOwnerId = captureResult.TargetOwnerId
            ?? captureResult.Captures
                .FirstOrDefault(capture => capture.IsTarget)
                ?.AuthorUserId;
        var snapshots = captureResult.Captures.Select(ToSnapshot).ToArray();

        ContentReport report;
        bool duplicate;

        try
        {
            (report, duplicate) = await context.InTransactionAsync(async token =>
            {

                var concurrent = await context.Tracked<ContentReport>().FirstOrDefaultAsync(
                    candidate => candidate.ReporterUserId == reporterUserId
                        && candidate.TargetKind == target.TargetKind
                        && candidate.TargetId == request.TargetId
                        && candidate.Status != ReportStatus.Withdrawn,
                    ct);

                if (concurrent is not null)
                {
                    return (concurrent, true);
                }

                var created = new ContentReport
                {
                    ReporterUserId = reporterUserId,
                    TargetKind = target.TargetKind,
                    TargetId = request.TargetId,
                    TargetOwnerId = targetOwnerId is { } owner && owner != Guid.Empty ? owner : null,
                    Reason = request.Reason,
                    Details = details,
                    Status = ReportStatus.Pending
                };

                context.Add(created);
                context.AddRange(
                    snapshots.Select(snapshot => new ReportedContentSnapshot
                    {
                        ContentReportId = created.Id,
                        AuthorUserId = snapshot.AuthorUserId,
                        Body = snapshot.Body,
                        OccurredAt = snapshot.OccurredAt,
                        IsTarget = snapshot.IsTarget
                    }));
                context.Add(new HiddenContent
                {
                    UserId = reporterUserId,
                    TargetKind = target.TargetKind,
                    TargetId = request.TargetId,
                    Source = HiddenContentSource.Report,
                    SourceId = created.Id
                });

                await context.SaveChangesAsync(ct);
                return (created, false);
            }, IsolationLevel.Serializable, ct);
        }
        catch (UniqueConstraintException exception) when (
            exception.Is("ix_content_report_reporter_user_id_target_kind_target_id"))
        {
            return ReportResult.Fail(ReportFailure.Duplicate);
        }

        if (duplicate)
        {
            return ReportResult.Fail(ReportFailure.Duplicate);
        }

        // The hidden row is committed. Invalidation must not be skipped by a request that is
        // cancelled immediately after that commit, or a stale cache could reveal the target.
        await cache.RemoveAsync(
            HiddenCacheKey(reporterUserId, target.TargetKind),
            CancellationToken.None);

        if (request.ClearHistory)
        {
            await ClearHistoryAsync(reporterUserId, target.TargetKind, request.TargetId, ct);
        }

        return ReportResult.Ok(ToClient(report));
    }

    public async Task<PagedResult<ClientContentReportDto>> ListAdminAsync(
        PageRequest paging,
        ReportStatus? status = null,
        string? targetKind = null,
        ReportReason? reason = null,
        SearchRequest? search = null,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default)
    {
        var query = reports.Query();
        var effectiveStatus = status ?? ReportStatus.Pending;
        query = query.Where(report => report.Status == effectiveStatus);

        if (!string.IsNullOrWhiteSpace(targetKind))
        {
            if (!registry.TryGet(targetKind, out var target))
            {
                throw new ArgumentException("The target kind is not reportable.", nameof(targetKind));
            }

            query = query.Where(report => report.TargetKind == target.TargetKind);
        }

        if (reason is { } reportReason)
        {
            query = query.Where(report => report.Reason == reportReason);
        }

        query = query.Search(search?.Field, search?.Keyword, audience is ResponseAudience.Admin ? typeof(AdminContentReportDto) : typeof(ClientContentReportDto));

        return await query.ToPagedResultAsync(
            paging,
            ReportProjection(audience),
            reports => reports.OrderBy(candidate => candidate.CreatedAt).ThenBy(candidate => candidate.Id),
            ct);
    }

    public async Task<ClientContentReportDto?> GetAdminAsync(
        Guid reportId,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default)
    {
        var report = await reports.Query()
            .Where(candidate => candidate.Id == reportId)
            .Select(ReportProjection(audience))
            .FirstOrDefaultAsync(ct);

        if (report is not AdminContentReportDto adminReport)
        {
            return report;
        }

        var linkedReports = await context.Query<ContentReport>()
            .Where(candidate => candidate.TargetKind == adminReport.TargetKind
                && candidate.TargetId == adminReport.TargetId)
            .OrderBy(candidate => candidate.CreatedAt)
            .ThenBy(candidate => candidate.Id)
            .Select(candidate => new AdminLinkedContentReportDto(
                candidate.Id,
                candidate.ReporterUserId,
                candidate.Reason,
                candidate.Details,
                candidate.Status,
                candidate.CreatedAt))
            .ToListAsync(ct);
        var snapshots = await context.Query<ReportedContentSnapshot>()
            .Where(snapshot => snapshot.ContentReportId == reportId)
            .OrderBy(snapshot => snapshot.OccurredAt)
            .ThenBy(snapshot => snapshot.Id)
            .Select(snapshot => new AdminReportedContentSnapshotDto(
                snapshot.AuthorUserId,
                snapshot.Body,
                snapshot.OccurredAt,
                snapshot.IsTarget))
            .ToListAsync(ct);
        var history = await context.Query<ContentReportDecision>()
            .Where(decision => decision.ContentReportId == reportId)
            .OrderBy(decision => decision.DecidedAt)
            .ThenBy(decision => decision.Id)
            .Select(decision => new AdminReportDecisionDto(
                decision.AdministratorUserId,
                decision.PreviousStatus,
                decision.NewStatus,
                decision.Note,
                decision.DecidedAt))
            .ToListAsync(ct);

        return new AdminContentReportDetailsDto(
            adminReport.Id,
            adminReport.TargetKind,
            adminReport.TargetId,
            adminReport.Reason,
            adminReport.Status,
            adminReport.CreatedAt,
            adminReport.ReporterUserId,
            adminReport.TargetOwnerId,
            adminReport.Details,
            adminReport.ReviewedByUserId,
            adminReport.ReviewedAt,
            adminReport.ReviewNote,
            linkedReports.Count,
            adminReport.IsGloballyRestricted,
            linkedReports,
            snapshots,
            history);
    }

    public async Task<bool> WithdrawAsync(
        Guid reporterUserId,
        string targetKind,
        Guid targetId,
        CancellationToken ct = default)
    {
        EnsureUserId(reporterUserId, nameof(reporterUserId));
        var normalizedKind = ReportTargetKind.Normalize(targetKind);
        if (normalizedKind is not ("post" or "article"))
        {
            throw new ArgumentException(
                "Only post and article reports can be withdrawn.",
                nameof(targetKind));
        }

        if (targetId == Guid.Empty)
        {
            return false;
        }

        var withdrawn = await context.InTransactionAsync(async token =>
        {

            var report = await context.Tracked<ContentReport>().FirstOrDefaultAsync(
                candidate => candidate.ReporterUserId == reporterUserId
                    && candidate.TargetKind == normalizedKind
                    && candidate.TargetId == targetId
                    && candidate.Status == ReportStatus.Pending,
                ct);

            if (report is null)
            {
                return false;
            }

            report.Status = ReportStatus.Withdrawn;
            var reportHide = await context.Tracked<HiddenContent>().FirstOrDefaultAsync(
                hidden => hidden.UserId == reporterUserId
                    && hidden.TargetKind == normalizedKind
                    && hidden.TargetId == targetId
                    && hidden.Source == HiddenContentSource.Report
                    && hidden.SourceId == report.Id,
                ct);
            if (reportHide is not null)
            {
                context.Remove(reportHide);
            }

            await context.SaveChangesAsync(ct);
            return true;
        }, IsolationLevel.Serializable, ct);

        if (withdrawn)
        {
            // The report and its exact reporter-owned hide are committed together. Do not let a
            // cancellation immediately after that commit leave a cache that still hides content.
            await cache.RemoveAsync(
                HiddenCacheKey(reporterUserId, normalizedKind),
                CancellationToken.None);
        }

        return withdrawn;
    }

    public async Task<bool> DecideAsync(
        Guid actingAdministratorId,
        Guid reportId,
        ReportDecisionRequest request,
        CancellationToken ct = default)
    {
        await EnsureModeratorAsync(actingAdministratorId, ct);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Status is ReportStatus.Pending || !Enum.IsDefined(request.Status))
        {
            throw new ArgumentException("A decision must move the report out of Pending.", nameof(request));
        }

        var note = string.IsNullOrWhiteSpace(request.ReviewNote) ? null : request.ReviewNote.Trim();
        var decision = await context.InTransactionAsync(async token =>
        {

            var selectedReport = await context.Tracked<ContentReport>()
                .FirstOrDefaultAsync(candidate => candidate.Id == reportId, ct);
            if (selectedReport is null)
            {
                return (
                    Found: false,
                    TargetKind: string.Empty,
                    TargetId: Guid.Empty,
                    Reports: Array.Empty<(Guid ReportId, Guid ReporterUserId)>());
            }

            if (selectedReport.Status is ReportStatus.Withdrawn)
            {
                throw new ArgumentException(
                    "A withdrawn report cannot receive an administrator decision.",
                    nameof(reportId));
            }

            var targetReports = request.Status is ReportStatus.Reviewed
                ? [selectedReport]
                : await context.Tracked<ContentReport>()
                    .Where(candidate => candidate.TargetKind == selectedReport.TargetKind
                        && candidate.TargetId == selectedReport.TargetId
                        && candidate.Status != ReportStatus.Withdrawn)
                    .ToListAsync(ct);
            var reportsToChange = targetReports
                .Where(report => report.Status != request.Status)
                .ToArray();

            if (reportsToChange.Length == 0)
            {
                throw new ArgumentException(
                    $"The transition from '{selectedReport.Status}' to '{request.Status}' is not allowed.",
                    nameof(request));
            }

            foreach (var report in reportsToChange)
            {
                if (!ReportDecisionRules.IsAllowed(report.Status, request.Status))
                {
                    throw new ArgumentException(
                        $"The transition from '{report.Status}' to '{request.Status}' is not allowed.",
                        nameof(request));
                }

                if (report.Status is ReportStatus.Dismissed
                    && request.Status is ReportStatus.ActionTaken
                    && note is null)
                {
                    throw new ArgumentException(
                        "A review note is required when reversing dismissed reports.",
                        nameof(request));
                }
            }

            var decidedAt = DateTimeOffset.UtcNow;
            foreach (var report in reportsToChange)
            {
                var previous = report.Status;
                report.Status = request.Status;
                report.ReviewedByUserId = actingAdministratorId;
                report.ReviewedAt = decidedAt;
                report.ReviewNote = note;
                context.Add(new ContentReportDecision
                {
                    ContentReportId = report.Id,
                    AdministratorUserId = actingAdministratorId,
                    PreviousStatus = previous,
                    NewStatus = request.Status,
                    Note = note,
                    DecidedAt = decidedAt
                });
            }

            var targetReportIds = targetReports.Select(report => report.Id).ToArray();
            if (request.Status is ReportStatus.Dismissed)
            {
                var reportHides = await context.Tracked<HiddenContent>()
                    .Where(hidden => hidden.TargetKind == selectedReport.TargetKind
                        && hidden.TargetId == selectedReport.TargetId
                        && hidden.Source == HiddenContentSource.Report
                        && targetReportIds.Contains(hidden.SourceId))
                    .ToListAsync(ct);
                context.RemoveRange(reportHides);
            }
            else if (request.Status is ReportStatus.ActionTaken)
            {
                var existingHideSourceIds = await context.Tracked<HiddenContent>()
                    .Where(hidden => hidden.TargetKind == selectedReport.TargetKind
                        && hidden.TargetId == selectedReport.TargetId
                        && hidden.Source == HiddenContentSource.Report
                        && targetReportIds.Contains(hidden.SourceId))
                    .Select(hidden => hidden.SourceId)
                    .ToHashSetAsync(ct);
                context.AddRange(targetReports
                    .Where(report => !existingHideSourceIds.Contains(report.Id))
                    .Select(report => new HiddenContent
                    {
                        UserId = report.ReporterUserId,
                        TargetKind = report.TargetKind,
                        TargetId = report.TargetId,
                        Source = HiddenContentSource.Report,
                        SourceId = report.Id
                    }));
            }

            await context.SaveChangesAsync(ct);
            return (
                Found: true,
                selectedReport.TargetKind,
                selectedReport.TargetId,
                Reports: targetReports
                    .Select(report => (ReportId: report.Id, ReporterUserId: report.ReporterUserId))
                    .ToArray());
        }, IsolationLevel.Serializable, ct);

        if (!decision.Found)
        {
            return false;
        }

        try
        {
            foreach (var reporterUserId in decision.Reports
                         .Select(report => report.ReporterUserId)
                         .Distinct())
            {
                await cache.RemoveAsync(
                    HiddenCacheKey(reporterUserId, decision.TargetKind),
                    CancellationToken.None);
            }
        }
        finally
        {
            // Status and reporter-hide changes committed together. Their caches must not remain
            // stale when the request is cancelled after that transaction.
            await restrictions.InvalidateAsync(decision.TargetKind, CancellationToken.None);
        }

        return true;
    }

    public async Task<PagedResult<ClientContentReportDto>> ListForUserAsync(
        Guid userId,
        PageRequest paging,
        bool asReporter,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default)
    {
        var query = reports.Query();
        query = asReporter
            ? query.Where(report => report.ReporterUserId == userId)
            : query.Where(report => report.TargetOwnerId == userId);

        return await query.ToPagedResultAsync(
            paging,
            ReportProjection(audience),
            reports => reports.OrderByDescending(report => report.CreatedAt).ThenBy(report => report.Id),
            ct);
    }

    private async Task<ReportableContentCaptureResult> CaptureAsync(
        ReportTargetDefinition target,
        Guid targetId,
        CancellationToken ct)
    {
        try
        {
            return await target.Source.CaptureAsync(targetId, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Capture failed for report target {TargetKind}/{TargetId}; filing without a snapshot.",
                target.TargetKind,
                targetId);
            // An adapter failure is deliberately not a target-not-found result. Filing remains
            // available while the source is degraded; an explicit NotFound result is the 404 path.
            return ReportableContentCaptureResult.ForFound();
        }
    }

    private static ReportedContentSnapshot ToSnapshot(ReportedContentCapture capture) => new()
    {
        AuthorUserId = capture.AuthorUserId,
        Body = (capture.Body ?? string.Empty)[..Math.Min(capture.Body?.Length ?? 0, 50_000)],
        OccurredAt = capture.OccurredAt,
        IsTarget = capture.IsTarget
    };

    private async Task ClearHistoryAsync(
        Guid reporterUserId,
        string targetKind,
        Guid targetId,
        CancellationToken ct)
    {
        foreach (var clearer in historyClearers.Where(candidate =>
                     string.Equals(candidate.TargetKind, targetKind, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                await clearer.ClearAsync(reporterUserId, targetId, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "History clear failed after report {TargetKind}/{TargetId}; the report remains filed.",
                    targetKind,
                    targetId);
            }
        }
    }

    private Expression<Func<ContentReport, ClientContentReportDto>> ReportProjection(
        ResponseAudience audience) =>
        audience is ResponseAudience.Admin ? AdminProjection() : ClientProjection();

    private Expression<Func<ContentReport, ClientContentReportDto>> AdminProjection()
    {
        return report => (ClientContentReportDto)new AdminContentReportDto(
            report.Id,
            report.TargetKind,
            report.TargetId,
            report.Reason,
            report.Status,
            report.CreatedAt,
            report.ReporterUserId,
            report.TargetOwnerId,
            report.Details,
            report.ReviewedByUserId,
            report.ReviewedAt,
            report.ReviewNote,
            context.Tracked<ContentReport>().Count(candidate =>
                candidate.TargetKind == report.TargetKind && candidate.TargetId == report.TargetId),
            context.Tracked<ContentReport>().Any(candidate =>
                candidate.TargetKind == report.TargetKind
                    && candidate.TargetId == report.TargetId
                    && candidate.Status == ReportStatus.ActionTaken));
    }

    private static Expression<Func<ContentReport, ClientContentReportDto>> ClientProjection() =>
        report => new ClientContentReportDto(
            report.Id,
            report.TargetKind,
            report.TargetId,
            report.Reason,
            report.Status,
            report.CreatedAt);

    private async Task EnsureModeratorAsync(Guid userId, CancellationToken ct)
    {
        if (!await authorization.HasPermissionAsync(userId, AccessPermissions.ManageModeration, ct))
        {
            throw new UnauthorizedAccessException();
        }
    }

    private static ClientContentReportDto ToClient(ContentReport report) => new(
        report.Id,
        report.TargetKind,
        report.TargetId,
        report.Reason,
        report.Status,
        report.CreatedAt);

    private static string HiddenCacheKey(Guid userId, string targetKind) =>
        $"moderation:v1:hidden:{userId}:{targetKind}";

    private static void EnsureUserId(Guid userId, string parameterName)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", parameterName);
        }
    }
}

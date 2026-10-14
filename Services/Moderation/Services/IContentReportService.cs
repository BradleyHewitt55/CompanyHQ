using CompanyHQ.Moderation.Contracts.Dtos;
using CompanyHQ.Moderation.Contracts.Enums;
using CompanyHQ.Moderation.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;

namespace CompanyHQ.Moderation.Services;

public interface IContentReportService
{
    Task<ReportResult> CreateAsync(
        Guid reporterUserId,
        CreateReportRequest request,
        CancellationToken ct = default);

    Task<PagedResult<ClientContentReportDto>> ListAdminAsync(
        PageRequest paging,
        ReportStatus? status = null,
        string? targetKind = null,
        ReportReason? reason = null,
        SearchRequest? search = null,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default);

    Task<ClientContentReportDto?> GetAdminAsync(
        Guid reportId,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default);

    Task<bool> DecideAsync(
        Guid actingAdministratorId,
        Guid reportId,
        ReportDecisionRequest request,
        CancellationToken ct = default);

    Task<PagedResult<ClientContentReportDto>> ListForUserAsync(
        Guid userId,
        PageRequest paging,
        bool asReporter,
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken ct = default);

    Task<bool> WithdrawAsync(
        Guid reporterUserId,
        string targetKind,
        Guid targetId,
        CancellationToken ct = default);
}

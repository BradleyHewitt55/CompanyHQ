
using CompanyHQ.Services.Moderation.Contracts.Ports;
using CompanyHQ.Services.Moderation.Contracts.Captures;
using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Moderation.Services;

/// <summary>Resolves accounts and captures only public, operator-safe profile evidence.</summary>
public sealed class UserReportableContentSource(
    IRepository<User> users) : IReportableContentSource
{
    public string TargetKind => "user";

    public IReadOnlyList<ReportReason> AllowedReasons { get; } =
    [
        ReportReason.Spam,
        ReportReason.Harassment,
        ReportReason.Impersonation,
        ReportReason.SexualContent,
        ReportReason.Violence,
        ReportReason.Other
    ];

    public async Task<ReportableContentCaptureResult> CaptureAsync(
        Guid targetId,
        CancellationToken ct = default)
    {
        var user = await users.Query()
            .Where(candidate => candidate.Id == targetId)
            .Select(candidate => new { candidate.Id, candidate.FirstName, candidate.LastName, candidate.Bio })
            .SingleOrDefaultAsync(ct);

        if (user is null)
        {
            return ReportableContentCaptureResult.NotFound;
        }

        var body = $"First name: {user.FirstName?.Trim() ?? "(not set)"}\n"
            + $"Last name: {user.LastName?.Trim() ?? "(not set)"}\n"
            + $"Bio: {user.Bio?.Trim() ?? "(not set)"}";

        return ReportableContentCaptureResult.ForFound(
            user.Id,
            [new ReportedContentCapture(user.Id, body, DateTimeOffset.UtcNow, IsTarget: true)]);
    }
}

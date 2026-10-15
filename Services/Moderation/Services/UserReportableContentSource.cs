using CompanyHQ.ServiceCatalog.Contracts;

using CompanyHQ.Moderation.Contracts.Ports;
using CompanyHQ.Moderation.Contracts.Captures;
using CompanyHQ.Moderation.Contracts.Enums;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Moderation.Services;

/// <summary>Resolves accounts and captures only public, operator-safe profile evidence.</summary>
public sealed class UserReportableContentSource(IServiceAvailability? availability,
    IRepository<User> users) : IReportableContentSource
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public UserReportableContentSource(
    IRepository<User> users) : this(null, users) { }

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
        await (availability?.RequireAsync("moderation", false, ct) ?? Task.CompletedTask);
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

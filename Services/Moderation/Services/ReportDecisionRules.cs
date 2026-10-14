using CompanyHQ.Moderation.Contracts.Enums;

namespace CompanyHQ.Moderation.Services;

internal static class ReportDecisionRules
{
    public static bool IsAllowed(ReportStatus previous, ReportStatus next) =>
        (previous, next) switch
        {
            (ReportStatus.Pending, ReportStatus.Reviewed or ReportStatus.ActionTaken or ReportStatus.Dismissed) => true,
            (ReportStatus.Reviewed, ReportStatus.ActionTaken or ReportStatus.Dismissed) => true,
            (ReportStatus.ActionTaken, ReportStatus.Dismissed) => true,
            (ReportStatus.Dismissed, ReportStatus.ActionTaken) => true,
            _ => false
        };
}

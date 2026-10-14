using CompanyHQ.Moderation.Contracts.Captures;
using CompanyHQ.Moderation.Contracts.Enums;

namespace CompanyHQ.Moderation.Contracts.Ports;

/// <summary>Consumer hook for resolving a target and copying frozen evidence without a Moderation-to-domain dependency.</summary>
public interface IReportableContentSource
{
    string TargetKind { get; }

    IReadOnlyList<ReportReason> AllowedReasons { get; }

    Task<ReportableContentCaptureResult> CaptureAsync(
        Guid targetId,
        CancellationToken ct = default);
}

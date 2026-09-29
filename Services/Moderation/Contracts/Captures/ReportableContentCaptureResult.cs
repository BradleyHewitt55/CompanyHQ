namespace CompanyHQ.Services.Moderation.Contracts.Captures;

/// <summary>Resolution and frozen evidence returned by a reportable content adapter.</summary>
public sealed record ReportableContentCaptureResult(
    bool Found,
    Guid? TargetOwnerId,
    IReadOnlyList<ReportedContentCapture> Captures)
{
    public static ReportableContentCaptureResult NotFound { get; } = new(false, null, []);

    public static ReportableContentCaptureResult ForFound(
        Guid? targetOwnerId = null,
        IReadOnlyList<ReportedContentCapture>? captures = null) =>
        new(true, targetOwnerId, captures ?? []);
}

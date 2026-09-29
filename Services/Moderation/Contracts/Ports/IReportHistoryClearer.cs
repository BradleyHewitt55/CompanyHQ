namespace CompanyHQ.Services.Moderation.Contracts.Ports;

/// <summary>Optional consumer hook for clearing one reporter's history after a report is filed.</summary>
public interface IReportHistoryClearer
{
    string TargetKind { get; }

    Task ClearAsync(
        Guid reporterUserId,
        Guid targetId,
        CancellationToken ct = default);
}

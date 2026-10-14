using CompanyHQ.Moderation.Contracts.Ports;
using CompanyHQ.Moderation.Contracts.Dtos;
using CompanyHQ.Moderation.Contracts.Enums;

namespace CompanyHQ.Moderation.Services;

/// <summary>Scoped allowlist of report targets contributed by the host's registered adapters.</summary>
public interface IReportTargetRegistry
{
    bool TryGet(string? targetKind, out ReportTargetDefinition definition);

    ReportOptionsDto? GetOptions(string? targetKind);
}

/// <summary>A normalized target kind, its adapter, and its immutable reason catalog.</summary>
public sealed record ReportTargetDefinition(
    string TargetKind,
    IReportableContentSource Source,
    IReadOnlyList<ReportReason> AllowedReasons)
{
    public bool Allows(ReportReason reason) => AllowedReasons.Contains(reason);
}

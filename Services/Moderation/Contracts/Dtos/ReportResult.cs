namespace CompanyHQ.Moderation.Contracts.Dtos;

/// <summary>Report creation outcome, kept free of HTTP concerns.</summary>
public readonly record struct ReportResult(ClientContentReportDto? Value, ReportFailure? Failure)
{
    public bool Succeeded => Failure is null;

    public static ReportResult Ok(ClientContentReportDto value) => new(value, null);

    public static ReportResult Fail(ReportFailure failure) => new(null, failure);
}

public enum ReportFailure
{
    Duplicate,
    TargetNotFound,
    SelfTarget
}

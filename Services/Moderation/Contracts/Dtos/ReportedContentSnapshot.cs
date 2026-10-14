namespace CompanyHQ.Moderation.Contracts.Dtos;

/// <summary>One frozen evidence row shown inline to an operator.</summary>
public sealed record AdminReportedContentSnapshotDto(
    Guid? AuthorUserId,
    string Body,
    DateTimeOffset OccurredAt,
    bool IsTarget);

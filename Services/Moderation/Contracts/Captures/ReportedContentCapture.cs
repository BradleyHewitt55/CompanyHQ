namespace CompanyHQ.Moderation.Contracts.Captures;

/// <summary>Evidence handed to Moderation by a reportable consumer.</summary>
public sealed record ReportedContentCapture(
    Guid? AuthorUserId,
    string Body,
    DateTimeOffset OccurredAt,
    bool IsTarget);

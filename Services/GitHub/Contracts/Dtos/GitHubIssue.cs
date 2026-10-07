namespace CompanyHQ.Services.GitHub.Contracts.Dtos;

/// <summary>One issue in the connected repository, as a manager sees it.</summary>
public sealed record AdminGitHubIssueDto(
    int Number,
    string Title,
    string Body,
    string Author,
    bool Open,
    string Url,
    int Comments,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminGitHubIssueCommentDto(
    long Id,
    string Body,
    string Author,
    DateTimeOffset CreatedAt);

public sealed record AdminGitHubIssueThreadDto(
    AdminGitHubIssueDto Issue,
    IReadOnlyList<AdminGitHubIssueCommentDto> Comments);

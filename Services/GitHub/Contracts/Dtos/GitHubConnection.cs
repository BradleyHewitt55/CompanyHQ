using System.Text.Json.Serialization;

namespace CompanyHQ.Services.GitHub.Contracts.Dtos;

/// <summary>What anyone may know: whether an issue can be filed right now.</summary>
[JsonDerivedType(typeof(AdminGitHubConnectionDto))]
public record ClientGitHubConnectionDto(bool IssuesEnabled);

/// <summary>The manager's view, which names the repository. The token is never in any shape.</summary>
public sealed record AdminGitHubConnectionDto(
    bool IssuesEnabled,
    string? Repository,
    DateTimeOffset? ConnectedAt)
    : ClientGitHubConnectionDto(IssuesEnabled);

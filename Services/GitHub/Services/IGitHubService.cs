using CompanyHQ.GitHub.Contracts.Dtos;
using CompanyHQ.GitHub.Contracts.Enums;
using CompanyHQ.GitHub.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;

namespace CompanyHQ.GitHub.Services;

/// <summary>
/// The connected repository and its issues. Every method that takes an acting user checks
/// <c>github.manage</c> on their role row and answers <see cref="GitHubError.Forbidden"/> without it.
/// </summary>
public interface IGitHubService
{
    Task<ClientGitHubConnectionDto> GetAsync(
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies the repository with GitHub before storing it, replacing any earlier connection.</summary>
    Task<GitHubError> ConnectAsync(
        Guid actingUserId,
        ConnectGitHubRepositoryRequest request,
        CancellationToken cancellationToken = default);

    Task<GitHubError> DisconnectAsync(Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Files an issue for another service. It takes no acting user: the caller has already decided
    /// the report may be filed, and owns the brake on how often.
    /// </summary>
    Task<GitHubError> SubmitIssueAsync(
        SubmitGitHubIssueRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Issues most recently updated first, open and closed, optionally matching a keyword.</summary>
    Task<(GitHubError Error, PagedResult<AdminGitHubIssueDto>? Issues)> ListIssuesAsync(
        Guid actingUserId,
        PageRequest paging,
        string? keyword = null,
        CancellationToken cancellationToken = default);

    Task<(GitHubError Error, AdminGitHubIssueThreadDto? Thread)> GetIssueAsync(
        Guid actingUserId,
        int number,
        CancellationToken cancellationToken = default);

    Task<GitHubError> CommentAsync(
        Guid actingUserId,
        int number,
        string body,
        CancellationToken cancellationToken = default);

    Task<GitHubError> CloseIssueAsync(
        Guid actingUserId,
        int number,
        CancellationToken cancellationToken = default);
}

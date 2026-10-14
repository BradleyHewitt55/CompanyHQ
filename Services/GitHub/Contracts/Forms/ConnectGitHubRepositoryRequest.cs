using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.GitHub.Contracts.Forms;

/// <summary>Body for <c>PUT /api/github</c>: an <c>owner/name</c> repository and a token that can reach it.</summary>
public sealed record ConnectGitHubRepositoryRequest(
    [Required, StringLength(320), RegularExpression(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")] string Repository,
    [Required, StringLength(500)] string AccessToken);

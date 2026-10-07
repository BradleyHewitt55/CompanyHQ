using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.GitHub.Contracts.Forms;

/// <summary>Body for <c>POST /api/github/issues/{number}/comments</c>.</summary>
public sealed record CommentGitHubIssueRequest(
    [Required, StringLength(4000, MinimumLength = 1)] string Body);

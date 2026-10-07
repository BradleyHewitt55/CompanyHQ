using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.GitHub.Contracts.Forms;

/// <summary>
/// One issue to file. Never bound from a request body here: the caller is whichever service
/// collected the report, and everything in it is that service's anonymous public input.
/// </summary>
/// <param name="Origin">Where the report was collected, written into the issue body as given.</param>
public sealed record SubmitGitHubIssueRequest(
    [Required, StringLength(40)] string Category,
    [Required, StringLength(160, MinimumLength = 5)] string Title,
    [Required, StringLength(4000, MinimumLength = 10)] string Message,
    [EmailAddress, StringLength(254)] string? Email = null,
    [StringLength(600)] string? Origin = null);

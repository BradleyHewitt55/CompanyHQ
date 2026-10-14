using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Auth.Contracts.Identity;
using CompanyHQ.GitHub.Contracts.Dtos;
using CompanyHQ.GitHub.Contracts.Enums;
using CompanyHQ.GitHub.Contracts.Forms;
using CompanyHQ.GitHub.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Users.Extensions;

namespace CompanyHQ.GitHub.Controllers;

/// <summary>The connected issue repository and its issues. Everything but the status read needs <c>github.manage</c>.</summary>
[ApiController]
[Route("api/github")]
[Produces("application/json")]
public sealed class GitHubController(IGitHubService github) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<ClientGitHubConnectionDto>(StatusCodes.Status200OK)]
    public Task<ClientGitHubConnectionDto> Get(CancellationToken cancellationToken = default) =>
        github.GetAsync(User.Audience(AccessPermissions.ManageGitHub), cancellationToken);

    [HttpPut]
    [Authorize(Policy = AccessPermissions.ManageGitHub)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Connect(
        ConnectGitHubRepositoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        var error = await github.ConnectAsync(actingUserId, request, cancellationToken);
        return error is GitHubError.Rejected
            ? Problem(
                title: "That repository cannot be connected.",
                detail: "Choose a repository this token can read that has Issues enabled.",
                statusCode: StatusCodes.Status400BadRequest)
            : Result(error, NoContent());
    }

    [HttpDelete]
    [Authorize(Policy = AccessPermissions.ManageGitHub)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        return Result(await github.DisconnectAsync(actingUserId, cancellationToken), NoContent());
    }

    /// <summary>The repository's issues, most recently updated first. <c>keyword</c> searches them on GitHub.</summary>
    [HttpGet("issues")]
    [Authorize(Policy = AccessPermissions.ManageGitHub)]
    [ProducesResponseType<PagedResult<AdminGitHubIssueDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListIssues(
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        var (error, issues) = await github.ListIssuesAsync(actingUserId, paging, search.Keyword, cancellationToken);
        return Result(error, Ok(issues));
    }

    [HttpGet("issues/{number:int}")]
    [Authorize(Policy = AccessPermissions.ManageGitHub)]
    [ProducesResponseType<AdminGitHubIssueThreadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIssue(int number, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        var (error, thread) = await github.GetIssueAsync(actingUserId, number, cancellationToken);
        return Result(error, Ok(thread));
    }

    [HttpPost("issues/{number:int}/comments")]
    [Authorize(Policy = AccessPermissions.ManageGitHub)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Comment(
        int number,
        CommentGitHubIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        return Result(
            await github.CommentAsync(actingUserId, number, request.Body, cancellationToken),
            StatusCode(StatusCodes.Status201Created));
    }

    [HttpPost("issues/{number:int}/close")]
    [Authorize(Policy = AccessPermissions.ManageGitHub)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseIssue(int number, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        return Result(await github.CloseIssueAsync(actingUserId, number, cancellationToken), NoContent());
    }

    private IActionResult Result(GitHubError error, IActionResult success) => error switch
    {
        GitHubError.None => success,
        GitHubError.Forbidden => Forbid(),
        GitHubError.NotFound => NotFound(),
        GitHubError.NotConfigured => Problem(
            title: "GitHub is not configured on this server.",
            statusCode: StatusCodes.Status503ServiceUnavailable),
        GitHubError.NotConnected => Problem(
            title: "No GitHub repository is connected.",
            statusCode: StatusCodes.Status503ServiceUnavailable),
        GitHubError.Rejected => Problem(
            title: "GitHub did not accept the request.",
            statusCode: StatusCodes.Status502BadGateway),
        _ => Problem(
            title: "GitHub is unavailable.",
            statusCode: StatusCodes.Status502BadGateway)
    };
}

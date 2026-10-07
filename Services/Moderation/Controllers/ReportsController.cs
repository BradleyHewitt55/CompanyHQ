using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using CompanyHQ.Services.Moderation.Contracts.Dtos;
using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Moderation.Contracts.Forms;
using CompanyHQ.Services.Moderation.Extensions;
using CompanyHQ.Services.Moderation.Services;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Extensions;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Moderation.Controllers;

/// <summary>Caller reports and the administrator moderation queue.</summary>
[ApiController]
[Route("api/reports")]
[Authorize]
[Produces("application/json")]
public sealed class ReportsController(
    IContentReportService reports,
    IReportTargetRegistry registry) : ControllerBase
{
    [HttpGet("options/{targetKind}")]
    [ProducesResponseType<ReportOptionsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ReportOptionsDto> Options(string targetKind)
    {
        var options = registry.GetOptions(targetKind);
        return options is null ? NotFound() : Ok(options);
    }

    [HttpPost]
    [EnableRateLimiting(ModerationServiceCollectionExtensions.ReportRateLimitPolicyName)]
    [ProducesResponseType<ClientContentReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientContentReportDto>> Create(
        CreateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } reporterUserId)
        {
            return Unauthorized();
        }

        try
        {
            var result = await reports.CreateAsync(reporterUserId, request, cancellationToken);

            return result.Failure switch
            {
                ReportFailure.Duplicate => Conflict(new ProblemDetails
                {
                    Title = "Report already filed.",
                    Detail = "You have already reported this target.",
                    Status = StatusCodes.Status409Conflict
                }),
                ReportFailure.TargetNotFound => NotFound(),
                ReportFailure.SelfTarget => BadRequest("You cannot report your own account."),
                _ => Ok(result.Value)
            };
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid report.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet("mine")]
    [ProducesResponseType<PagedResult<ClientContentReportDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ClientContentReportDto>>> ListMine(
        [FromQuery] PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await reports.ListForUserAsync(
            userId,
            paging,
            asReporter: true,
            ct: cancellationToken));
    }

    [HttpDelete("{targetKind}/{targetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(
        string targetKind,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } reporterUserId)
        {
            return Unauthorized();
        }

        try
        {
            return await reports.WithdrawAsync(
                reporterUserId,
                targetKind,
                targetId,
                cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid report withdrawal.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet]
    [Authorize(Policy = AccessPermissions.ManageModeration)]
    [ProducesResponseType<PagedResult<ClientContentReportDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ClientContentReportDto>>> List(
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        [FromQuery] ReportStatus? status = null,
        [FromQuery] string? targetKind = null,
        [FromQuery] ReportReason? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await reports.ListAdminAsync(
                paging,
                status,
                targetKind,
                reason,
                search,
                User.Audience(AccessPermissions.ManageModeration),
                cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("{id:guid}/decision")]
    [Authorize(Policy = AccessPermissions.ManageModeration)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Decide(
        Guid id,
        ReportDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } administratorId)
        {
            return Unauthorized();
        }

        try
        {
            if (!await reports.DecideAsync(administratorId, id, request, cancellationToken))
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}

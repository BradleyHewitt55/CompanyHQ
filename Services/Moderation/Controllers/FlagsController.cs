using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Services.Moderation.Contracts.Dtos;
using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Moderation.Contracts.Forms;
using CompanyHQ.Services.Moderation.Services;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Extensions;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Moderation.Controllers;

/// <summary>The moderation queue for standing account flags.</summary>
[ApiController]
[Route("api/flags")]
[Authorize(Policy = AccessPermissions.ManageModeration)]
[Produces("application/json")]
public sealed class FlagsController(IAccountFlagService flags) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientAccountFlagDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ClientAccountFlagDto>>> List(
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        [FromQuery] AccountFlagStatus? status = null,
        [FromQuery] string? kind = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await flags.ListAdminAsync(
                paging,
                status,
                kind,
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Decide(
        Guid id,
        FlagDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } administratorId)
        {
            return Unauthorized();
        }

        try
        {
            if (!await flags.DecideAsync(administratorId, id, request, cancellationToken))
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

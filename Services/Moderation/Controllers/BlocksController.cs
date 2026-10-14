using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Moderation.Contracts.Dtos;
using CompanyHQ.Moderation.Contracts.Forms;
using CompanyHQ.Moderation.Services;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Moderation.Controllers;

/// <summary>The caller's block actions. Inbound blocks are deliberately absent from the list route.</summary>
[ApiController]
[Route("api/blocks")]
[Authorize]
[Produces("application/json")]
public sealed class BlocksController(IBlockService blocks) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ClientUserBlockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientUserBlockDto>> Create(
        BlockUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } blockerUserId)
        {
            return Unauthorized();
        }

        var result = await blocks.BlockAsync(blockerUserId, request, cancellationToken);

        return result.Failure switch
        {
            BlockFailure.Self => BadRequest("You cannot block your own account."),
            BlockFailure.NotFound => NotFound(),
            _ => Ok(result.Value)
        };
    }

    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } blockerUserId)
        {
            return Unauthorized();
        }

        return await blocks.UnblockAsync(blockerUserId, userId, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<ClientUserBlockDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ClientUserBlockDto>>> List(
        [FromQuery] PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } blockerUserId)
        {
            return Unauthorized();
        }

        return Ok(await blocks.ListOutboundAsync(blockerUserId, paging, cancellationToken));
    }
}

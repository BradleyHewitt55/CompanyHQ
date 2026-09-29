using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Services.Moderation.Contracts.Dtos;
using CompanyHQ.Services.Moderation.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Moderation.Controllers;

/// <summary>The caller's hidden target set for client-side reconciliation.</summary>
[ApiController]
[Route("api/hidden")]
[Authorize]
[Produces("application/json")]
public sealed class HiddenController(IContentHider hider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientHiddenContentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ClientHiddenContentDto>>> List(
        [FromQuery] PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await hider.ListAsync(userId, paging, cancellationToken));
    }

    [HttpDelete("{targetKind}/{targetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        string targetKind,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            return await hider.UnhideAsync(userId, targetKind, targetId, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Services.Mcp.Contracts.Dtos;
using CompanyHQ.Services.Mcp.Contracts.Enums;
using CompanyHQ.Services.Mcp.Contracts.Forms;
using CompanyHQ.Services.Mcp.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Mcp.Controllers;

/// <summary>Key lifecycle. Every route is bearer-only and scoped to the caller's own keys by default.</summary>
[ApiController]
[Authorize]
[Route("api/agent-keys")]
[Produces("application/json")]
public sealed class AgentKeysController(IAgentKeyService agentKeys) : ControllerBase
{
    /// <summary>The one moment the plaintext key and the row coexist. Never shown again.</summary>
    [HttpPost]
    [ProducesResponseType<IssuedAgentKeyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IssuedAgentKeyDto>> Issue(
        IssueAgentKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        AgentKeyOutcome outcome;
        try { outcome = await agentKeys.IssueAsync(userId, request, cancellationToken); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }

        return outcome.Succeeded
            ? Created($"/api/agent-keys/{outcome.Key!.Id}", outcome.Key)
            : Problem(detail: "The account no longer exists.", statusCode: StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// The caller's own keys by default. <c>?userId=</c> asks for another user's keys in the admin
    /// shape — gated here, not in routing, because the two shapes share one route on purpose.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientAgentKeyDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? userId,
        [FromQuery] PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } callerId)
        {
            return Unauthorized();
        }

        if (userId is null || userId == callerId)
        {
            return Ok(await agentKeys.ListMineAsync(callerId, paging, cancellationToken));
        }

        if (!User.HasPermission(AccessPermissions.ReadPrivateUsers))
        {
            return Forbid();
        }

        return Ok(await agentKeys.ListForUserAsync(userId.Value, paging, cancellationToken));
    }

    /// <summary>404 for a key that isn't the caller's and the caller isn't an administrator — never confirms it exists.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } callerId)
        {
            return Unauthorized();
        }

        var isAdmin = User.HasPermission(AccessPermissions.ManageUsers);
        var revoked = await agentKeys.RevokeAsync(callerId, isAdmin, id, cancellationToken);

        return revoked ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(Guid id, AssignAgentRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } callerId) return Unauthorized();
        try
        {
            var updated = await agentKeys.AssignRoleAsync(callerId,
                User.HasPermission(AccessPermissions.ManageUsers), id,
                request.AccessRoleId, cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
